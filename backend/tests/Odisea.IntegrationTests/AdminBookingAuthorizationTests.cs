using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Booking.Features.Bookings;

namespace Odisea.IntegrationTests;

/// Who may do what on /api/v1/admin/bookings: agencies nothing, operators the daily work,
/// admins the irreversible money decisions.
[Collection(ApiCollection.Name)]
public class AdminBookingAuthorizationTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Agencies_cannot_reach_operator_oversight()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var booking = await AgencyFlow.CreateDraftAsync(agent);

        await (await agent.GetAsync("/api/v1/admin/bookings")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await agent.GetAsync($"/api/v1/admin/bookings/{booking.Id}")).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await OperatorFlow.ConfirmAsync(agent, booking.Id)).ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operators_without_the_admin_role_cannot_confirm_cancel_or_void()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var admin = factory.ClientAs(TestIdentity.Admin);
        var booking = await AgencyFlow.SubmittedBookingAsync(agent);

        await (await OperatorFlow.ConfirmAsync(ops, booking.Id)).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await ops.PostAsJsonAsync($"/api/v1/admin/bookings/{booking.Id}/cancel",
            new OperatorNoteRequest("not allowed"), Json.Options)).ShouldBeAsync(HttpStatusCode.Forbidden);

        // Recording a payment is daily work; voiding it is not.
        var recorded = await OperatorFlow.RecordPaymentAsync(ops, booking.Id, 10m);
        await recorded.ShouldBeAsync(HttpStatusCode.Created);
        var payment = (await OperatorFlow.ReadAsync(recorded)).Payments.Single();

        await (await OperatorFlow.VoidPaymentAsync(ops, booking.Id, payment.Id)).ShouldBeAsync(HttpStatusCode.Forbidden);
        var voided = await OperatorFlow.VoidPaymentAsync(admin, booking.Id, payment.Id);
        await voided.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(0m, (await OperatorFlow.ReadAsync(voided)).PaidAmount);
    }

    [Fact]
    public async Task Admin_cannot_confirm_an_unpaid_reservation()
    {
        var booking = await AgencyFlow.SubmittedBookingAsync(factory.ClientAs(TestIdentity.Agent));

        var response = await OperatorFlow.ConfirmAsync(factory.ClientAs(TestIdentity.Admin), booking.Id);

        await response.ShouldBeAsync(HttpStatusCode.Conflict);
        Assert.Contains("Balance due", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Unknown_bookings_are_404_for_operators()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        await (await admin.GetAsync($"/api/v1/admin/bookings/{Guid.NewGuid()}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await OperatorFlow.ConfirmAsync(admin, Guid.NewGuid())).ShouldBeAsync(HttpStatusCode.NotFound);
    }
}
