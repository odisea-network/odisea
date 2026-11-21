using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Workspace.Domain.Ledger;
using Odisea.Modules.Workspace.Features.ExternalReservations;
using Odisea.Modules.Workspace.Features.Ledger;

namespace Odisea.IntegrationTests;

[Collection("api")]
public class LedgerTests(OdiseaApiFactory factory)
{
    private const string Reservations = "/api/v1/agency/ledger/reservations";
    private const string Operators = "/api/v1/agency/tour-operators";

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.ShouldBeAsync(expected);
        var dto = await response.Content.ReadFromJsonAsync<T>(Json.Options);
        Assert.NotNull(dto);
        return dto;
    }

    private static async Task ShouldBeCodedAsync(HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        await response.ShouldBeAsync(expected);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(code, problem.GetProperty("code").GetString());
    }

    private static async Task<TourOperatorDto> NewOperatorAsync(HttpClient client) =>
        await ReadAsync<TourOperatorDto>(
            await client.PostAsJsonAsync(Operators, new SaveTourOperatorRequest($"Alfa Tours {Guid.NewGuid():N}", null, null, null), Json.Options),
            HttpStatusCode.Created);

    private static CreateLedgerReservationRequest Request(Guid operatorId, string reference) => new(
        operatorId, reference, "Georgi Ivanov", new DateOnly(2026, 11, 20), new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 8),
        2000m, "EUR", null, null, null,
        new CommissionRequest(CommissionType.Percent, 7m, CommissionBase.OperatorTotal, null),
        [
            new(LedgerInstallmentLabel.Deposit, 600m, new DateOnly(2020, 12, 1)),
            new(LedgerInstallmentLabel.Balance, 1400m, new DateOnly(2099, 1, 15)),
        ]);

    private static RecordLedgerPaymentRequest Payment(decimal amount, Guid? key = null) =>
        new(amount, null, new DateOnly(2026, 11, 21), LedgerPaymentMethod.BankTransfer, "TRF-1", null, key);

    [Fact]
    public async Task Agent_registers_a_reservation_pays_part_of_the_deposit_and_sees_the_balance_everywhere()
    {
        var (agent, agencyId) = await factory.ClientForNewAgencyUserAsync(AgencyPlan.Ledger, AgencyUserRole.Agent);
        var (admin, _) = await factory.ClientForNewAgencyUserAsync(AgencyPlan.Ledger, AgencyUserRole.AgencyAdmin, agencyId);
        var tourOperator = await NewOperatorAsync(agent);
        var reference = $"TO-{Guid.NewGuid():N}"[..16];

        var created = await ReadAsync<LedgerReservationDto>(
            await agent.PostAsJsonAsync(Reservations, Request(tourOperator.Id, reference), Json.Options), HttpStatusCode.Created);
        Assert.Equal(tourOperator.Name, created.TourOperatorName);
        Assert.Equal(140m, created.Commission!.ExpectedAmount);
        Assert.NotEqual(0u, created.Version);

        await ShouldBeCodedAsync(await agent.PostAsJsonAsync(Reservations, Request(tourOperator.Id, reference), Json.Options),
            HttpStatusCode.Conflict, "reference_taken");

        // The same key twice: one payment.
        var key = Guid.NewGuid();
        await ReadAsync<LedgerReservationDto>(await agent.PostAsJsonAsync($"{Reservations}/{created.Id}/payments", Payment(400m, key), Json.Options));
        var paid = await ReadAsync<LedgerReservationDto>(
            await agent.PostAsJsonAsync($"{Reservations}/{created.Id}/payments", Payment(400m, key), Json.Options));
        Assert.Single(paid.Payments);
        Assert.Equal(1600m, paid.Outstanding);
        Assert.Equal(InstallmentPaymentStatus.PartiallyPaid, paid.Installments[0].Status);
        Assert.Equal(200m, paid.Installments[0].RemainingAmount);
        Assert.True(paid.Installments[0].IsOverdue);
        Assert.False(paid.Installments[1].IsOverdue);
        Assert.Equal(CommissionStatus.Expected, paid.Commission!.Status);
        Assert.NotEqual(created.Version, paid.Version);

        var list = await ReadAsync<PagedResult<LedgerReservationSummaryDto>>(await agent.GetAsync($"{Reservations}?q={reference}"));
        var row = Assert.Single(list.Items);
        Assert.Equal(400m, row.PaidTotal);
        Assert.Equal(1600m, row.Outstanding);
        Assert.Equal(200m, row.OverdueAmount);

        // A stale version is refused; the current one goes through.
        var stale = new ChangeLedgerStatusRequest(created.Version, LedgerReservationStatus.Completed, null);
        await ShouldBeCodedAsync(await agent.PostAsJsonAsync($"{Reservations}/{created.Id}/status", stale, Json.Options),
            HttpStatusCode.Conflict, "stale_version");
        var completed = await ReadAsync<LedgerReservationDto>(await agent.PostAsJsonAsync($"{Reservations}/{created.Id}/status",
            stale with { Version = paid.Version }, Json.Options));
        Assert.Equal(LedgerReservationStatus.Completed, completed.Status);

        // Corrections to recorded money are for the agency admin.
        var paymentId = paid.Payments[0].Id;
        await (await agent.PostAsJsonAsync($"{Reservations}/{created.Id}/payments/{paymentId}/void", new VoidLedgerPaymentRequest("typo"), Json.Options))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        var schedule = new ReplaceScheduleRequest(completed.Version,
            [new(LedgerInstallmentLabel.Other, 2000m, new DateOnly(2099, 1, 1))], "operator moved the dates");
        await ShouldBeCodedAsync(await agent.PutAsJsonAsync($"{Reservations}/{created.Id}/schedule", schedule, Json.Options),
            HttpStatusCode.Forbidden, "admin_required");
        var rescheduled = await ReadAsync<LedgerReservationDto>(
            await admin.PutAsJsonAsync($"{Reservations}/{created.Id}/schedule", schedule, Json.Options));
        Assert.Equal(400m, Assert.Single(rescheduled.Installments).PaidAmount);
        var voided = await ReadAsync<LedgerReservationDto>(await admin.PostAsJsonAsync(
            $"{Reservations}/{created.Id}/payments/{paymentId}/void", new VoidLedgerPaymentRequest("typo"), Json.Options));
        Assert.Equal(2000m, voided.Outstanding);
        Assert.True(Assert.Single(voided.Payments).IsVoided);

        var history = await ReadAsync<List<LedgerAuditEventDto>>(await agent.GetAsync($"{Reservations}/{created.Id}/history"));
        Assert.Equal(["PaymentVoided", "ScheduleReplaced", "StatusChanged", "PaymentRecorded", "Created"], history.Select(e => e.Action));
        Assert.Equal("typo", history[0].Reason);
        Assert.Equal(1400m, history[1].Changes.GetProperty("installments").GetProperty("from")[1].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Another_agency_gets_404_for_everything_that_is_not_its_own()
    {
        var (owner, _) = await factory.ClientForNewAgencyUserAsync(AgencyPlan.Ledger, AgencyUserRole.AgencyAdmin);
        var (stranger, _) = await factory.ClientForNewAgencyUserAsync(AgencyPlan.Workspace, AgencyUserRole.AgencyAdmin);
        var tourOperator = await NewOperatorAsync(owner);
        var created = await ReadAsync<LedgerReservationDto>(
            await owner.PostAsJsonAsync(Reservations, Request(tourOperator.Id, "PRIVATE-1"), Json.Options), HttpStatusCode.Created);
        var paid = await ReadAsync<LedgerReservationDto>(
            await owner.PostAsJsonAsync($"{Reservations}/{created.Id}/payments", Payment(100m), Json.Options));

        await (await stranger.GetAsync($"{Reservations}/{created.Id}")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await stranger.GetAsync($"{Reservations}/{created.Id}/history")).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await stranger.PostAsJsonAsync($"{Reservations}/{created.Id}/payments", Payment(50m), Json.Options))
            .ShouldBeAsync(HttpStatusCode.NotFound);
        await (await stranger.PostAsJsonAsync($"{Reservations}/{created.Id}/payments/{paid.Payments[0].Id}/void",
            new VoidLedgerPaymentRequest("x"), Json.Options)).ShouldBeAsync(HttpStatusCode.NotFound);
        await (await stranger.PutAsJsonAsync($"{Operators}/{tourOperator.Id}", new SaveTourOperatorRequest("Mine now", null, null, null), Json.Options))
            .ShouldBeAsync(HttpStatusCode.NotFound);
        await ShouldBeCodedAsync(await stranger.PostAsJsonAsync(Reservations, Request(tourOperator.Id, "THEIRS-1"), Json.Options),
            HttpStatusCode.UnprocessableEntity, "operator_unknown");

        Assert.Empty((await ReadAsync<PagedResult<LedgerReservationSummaryDto>>(await stranger.GetAsync(Reservations))).Items);
        Assert.Empty(await ReadAsync<List<TourOperatorDto>>(await stranger.GetAsync($"{Operators}?includeArchived=true")));
    }

    [Fact]
    public async Task The_ledger_is_closed_to_core_agencies_operators_and_anonymous_callers()
    {
        var (core, _) = await factory.ClientForNewAgencyUserAsync(AgencyPlan.Core, AgencyUserRole.AgencyAdmin);

        var refused = await core.PostAsJsonAsync(Operators, new SaveTourOperatorRequest("Alfa", null, null, null), Json.Options);
        await refused.ShouldBeAsync(HttpStatusCode.Forbidden);
        Assert.Contains("feature-not-in-plan", await refused.Content.ReadAsStringAsync());
        await (await core.GetAsync(Reservations)).ShouldBeAsync(HttpStatusCode.OK);

        await (await factory.ClientAs(TestIdentity.Admin).GetAsync(Reservations)).ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await factory.AnonymousClient().GetAsync(Reservations)).ShouldBeAsync(HttpStatusCode.Unauthorized);
        await (await factory.AnonymousClient().GetAsync(Operators)).ShouldBeAsync(HttpStatusCode.Unauthorized);
    }
}
