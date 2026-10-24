using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Documents.Features.Issuing;

namespace Odisea.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingFlowTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Reservation_flows_from_search_to_invoice()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var admin = factory.ClientAs(TestIdentity.Admin);

        // Agency: search → draft → re-price → submit; the provider holds the reservation.
        var submitted = await AgencyFlow.SubmittedBookingAsync(agent);
        Assert.NotNull(submitted.PaymentDueAt);
        Assert.StartsWith("MOCK-", submitted.ProviderBookingRef);
        Assert.Equal(submitted.SellPrice, submitted.BalanceDue);
        Assert.Equal("System", submitted.History[^1].Actor);

        // Operator sees the net side; the agency never does.
        var detail = await OperatorFlow.GetAsync(admin, submitted.Id);
        Assert.Equal("AwaitingPayment", detail.Status);
        Assert.NotNull(detail.NetAmount);
        Assert.NotNull(detail.Breakdown);
        Assert.True(detail.NetAmount < detail.SellAmount);
        Assert.Equal("AB1234567", detail.Passengers[0].DocumentNumber);

        // Nobody confirms before the money is in.
        await (await OperatorFlow.ConfirmAsync(admin, submitted.Id)).ShouldBeAsync(HttpStatusCode.Conflict);

        var paid = await OperatorFlow.RecordPaymentAsync(ops, submitted.Id, detail.SellAmount!.Value);
        await paid.ShouldBeAsync(HttpStatusCode.Created);
        Assert.Contains(submitted.Id.ToString(), paid.Headers.Location?.ToString());

        var confirmed = await OperatorFlow.ConfirmAsync(admin, submitted.Id);
        await confirmed.ShouldBeAsync(HttpStatusCode.OK);
        var confirmedDto = await OperatorFlow.ReadAsync(confirmed);
        Assert.Equal("Confirmed", confirmedDto.Status);
        Assert.Equal(0m, confirmedDto.BalanceDue);

        // The agency sees the outcome, but may no longer cancel on its own.
        var agencyView = await AgencyFlow.GetAsync(agent, submitted.Id);
        Assert.Equal("Confirmed", agencyView.Status);
        Assert.Equal(agencyView.SellPrice, agencyView.PaidAmount);
        Assert.Equal("Operator", agencyView.History[^1].Actor);
        await (await agent.PostAsync($"/api/v1/bookings/{submitted.Id}/cancel", null)).ShouldBeAsync(HttpStatusCode.Conflict);

        // Documents issues the invoice against the confirmed booking.
        var invoiced = await admin.PostAsJsonAsync("/api/v1/documents/invoices",
            new IssueInvoiceRequest(submitted.Id), Json.Options);
        await invoiced.ShouldBeAsync(HttpStatusCode.Created);
        var invoice = await invoiced.Content.ReadFromJsonAsync<FinancialDocumentDto>(Json.Options);
        Assert.NotNull(invoice);
        Assert.Equal(submitted.Ref, invoice.BookingRef);
        Assert.Equal("Issued", invoice.Status);

        var agencyDocuments = await agent.GetFromJsonAsync<List<FinancialDocumentDto>>("/api/v1/agency/documents", Json.Options);
        Assert.NotNull(agencyDocuments);
        Assert.Contains(agencyDocuments, d => d.Id == invoice.Id);
    }

    [Fact]
    public async Task Submit_with_incomplete_passengers_is_rejected_without_leaking_data()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var passengers = AgencyFlow.TwoAdults();
        passengers[1] = passengers[1] with { Nationality = null, DocumentType = null, DocumentNumber = null };
        var draft = await AgencyFlow.CreateDraftAsync(agent, passengers);
        await AgencyFlow.RePriceAsync(agent, draft.Id);

        var response = await AgencyFlow.SubmitAsync(agent, draft.Id);

        await response.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Passenger 2", body);
        Assert.DoesNotContain("AB1234567", body);
        Assert.DoesNotContain("Petrova", body);
        Assert.Equal("PriceConfirmed", (await AgencyFlow.GetAsync(agent, draft.Id)).Status);
    }

    [Fact]
    public async Task Agency_can_withdraw_before_payment()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var submitted = await AgencyFlow.SubmittedBookingAsync(agent);

        var response = await agent.PostAsync($"/api/v1/bookings/{submitted.Id}/cancel", null);

        await response.ShouldBeAsync(HttpStatusCode.OK);
        var cancelled = await response.Content.ReadFromJsonAsync<BookingDto>(Json.Options);
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Agency", cancelled.History[^1].Actor);
    }

    [Fact]
    public async Task Operator_list_filters_by_status_and_pages()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        await AgencyFlow.SubmittedBookingAsync(factory.ClientAs(TestIdentity.Agent));

        var page = await admin.GetFromJsonAsync<PagedResult<OperatorBookingSummaryDto>>(
            "/api/v1/admin/bookings?status=AwaitingPayment&pageSize=1", Json.Options);
        Assert.NotNull(page);
        Assert.True(page.TotalCount >= 1);
        Assert.Single(page.Items);
        Assert.Equal("AwaitingPayment", page.Items[0].Status);

        var elsewhere = await admin.GetFromJsonAsync<PagedResult<OperatorBookingSummaryDto>>(
            $"/api/v1/admin/bookings?agencyId={Guid.NewGuid()}", Json.Options);
        Assert.NotNull(elsewhere);
        Assert.Equal(0, elsewhere.TotalCount);

        await (await admin.GetAsync("/api/v1/admin/bookings?pageSize=500")).ShouldBeAsync(HttpStatusCode.BadRequest);
    }
}
