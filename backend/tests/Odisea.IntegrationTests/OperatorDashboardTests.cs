using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Booking.Features.Bookings;

namespace Odisea.IntegrationTests;

/// /api/v1/admin/dashboard: operator-only, and it reflects the reservations that need a decision.
[Collection(ApiCollection.Name)]
public class OperatorDashboardTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Agencies_cannot_see_the_dashboard()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        await (await agent.GetAsync("/api/v1/admin/dashboard")).ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_a_window_outside_one_to_ninety_days()
    {
        var ops = factory.ClientAs(TestIdentity.Operator);
        await (await ops.GetAsync("/api/v1/admin/dashboard?days=0")).ShouldBeAsync(HttpStatusCode.BadRequest);
        await (await ops.GetAsync("/api/v1/admin/dashboard?days=91")).ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_fully_paid_reservation_is_ready_to_confirm_and_sits_in_the_queue()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var booking = await AgencyFlow.SubmittedBookingAsync(agent);
        await (await OperatorFlow.RecordPaymentAsync(ops, booking.Id, booking.BalanceDue)).ShouldBeAsync(HttpStatusCode.Created);

        var dashboard = await ops.GetFromJsonAsync<OperatorDashboardDto>("/api/v1/admin/dashboard?days=7", Json.Options);

        Assert.NotNull(dashboard);
        Assert.Equal(7, dashboard.Days);
        Assert.True(dashboard.ReadyToConfirm.Count >= 1);
        Assert.True(dashboard.ReadyToConfirm.Amount >= booking.SellPrice);
        var item = Assert.Single(dashboard.Queue, q => q.Id == booking.Id);
        Assert.Equal("ready", item.Kind);
        Assert.Equal(booking.SellPrice, item.Amount);
        Assert.True(dashboard.Created >= 1);
        Assert.Equal("EUR", dashboard.Currency);
    }

    [Fact]
    public async Task An_unpaid_reservation_with_a_far_deadline_is_not_in_the_queue()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var booking = await AgencyFlow.SubmittedBookingAsync(agent);

        var dashboard = await ops.GetFromJsonAsync<OperatorDashboardDto>("/api/v1/admin/dashboard", Json.Options);

        Assert.NotNull(dashboard);
        // The mock supplier gives three days to pay: neither overdue, nor due within 48 h, nor partially paid.
        Assert.DoesNotContain(dashboard.Queue, q => q.Id == booking.Id);
    }
}
