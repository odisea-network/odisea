using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Features.Maintenance;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Notifications.Infrastructure;

namespace Odisea.IntegrationTests;

/// The maintenance pass over real Postgres: expiry of unpaid holds, release retries, reminders.
/// The timer is off in tests (Booking:Maintenance:Enabled=false); each test runs one pass itself.
[Collection(ApiCollection.Name)]
public class BookingMaintenanceTests(OdiseaApiFactory factory)
{
    private async Task<BookingMaintenanceResult> RunPassAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BookingMaintenanceRun>().RunAsync(CancellationToken.None);
    }

    private async Task UpdateAsync(Guid bookingId, Func<IQueryable<Booking>, Task> update)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await update(db.Bookings.Where(b => b.Id == bookingId));
    }

    [Fact]
    public async Task An_unpaid_hold_past_its_deadline_expires_as_system_and_the_supplier_lets_go()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var booking = await AgencyFlow.SubmittedBookingAsync(agent);
        await UpdateAsync(booking.Id, q => q.ExecuteUpdateAsync(s =>
            s.SetProperty(b => b.PaymentDueAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        await RunPassAsync();

        var stored = await OperatorFlow.GetAsync(ops, booking.Id);
        Assert.Equal("Expired", stored.Status);
        Assert.Equal("System", stored.History[^1].ActorType);
        Assert.Equal("Released", stored.ProviderRelease);
    }

    [Fact]
    public async Task A_partly_paid_hold_past_its_deadline_stays_with_the_operator()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var booking = await AgencyFlow.SubmittedBookingAsync(agent);
        await (await OperatorFlow.RecordPaymentAsync(ops, booking.Id, 10m)).ShouldBeAsync(HttpStatusCode.Created);
        await UpdateAsync(booking.Id, q => q.ExecuteUpdateAsync(s =>
            s.SetProperty(b => b.PaymentDueAt, DateTimeOffset.UtcNow.AddMinutes(-1))));

        await RunPassAsync();

        Assert.Equal("AwaitingPayment", (await OperatorFlow.GetAsync(ops, booking.Id)).Status);
    }

    [Fact]
    public async Task Pending_releases_are_retried_once_their_backoff_has_passed()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var ops = factory.ClientAs(TestIdentity.Operator);
        var due = await AgencyFlow.SubmittedBookingAsync(agent);
        var recent = await AgencyFlow.SubmittedBookingAsync(agent);
        foreach (var id in new[] { due.Id, recent.Id })
        {
            var rejected = await ops.PostAsJsonAsync($"/api/v1/admin/bookings/{id}/reject",
                new OperatorNoteRequest("Supplier was down"), Json.Options);
            await rejected.ShouldBeAsync(HttpStatusCode.OK);
        }
        // As if the first release attempt had hit an unreachable supplier: one long ago, one just now.
        await UpdateAsync(due.Id, q => q.ExecuteUpdateAsync(s => s
            .SetProperty(b => b.ProviderRelease, ProviderReleaseStatus.Pending)
            .SetProperty(b => b.ProviderReleaseAttempts, 1)
            .SetProperty(b => b.ProviderReleaseUpdatedAt, DateTimeOffset.UtcNow.AddHours(-1))));
        await UpdateAsync(recent.Id, q => q.ExecuteUpdateAsync(s => s
            .SetProperty(b => b.ProviderRelease, ProviderReleaseStatus.Pending)
            .SetProperty(b => b.ProviderReleaseAttempts, 1)
            .SetProperty(b => b.ProviderReleaseUpdatedAt, DateTimeOffset.UtcNow)));

        await RunPassAsync();

        var retried = await OperatorFlow.GetAsync(ops, due.Id);
        Assert.Equal(("Released", 2), (retried.ProviderRelease, retried.ProviderReleaseAttempts));
        Assert.Equal("Pending", (await OperatorFlow.GetAsync(ops, recent.Id)).ProviderRelease);
    }

    [Fact]
    public async Task The_agency_is_reminded_once_before_the_deadline()
    {
        var agent = factory.ClientAs(TestIdentity.Agent);
        var booking = await AgencyFlow.SubmittedBookingAsync(agent);
        await UpdateAsync(booking.Id, q => q.ExecuteUpdateAsync(s => s
            .SetProperty(b => b.PaymentDueAt, DateTimeOffset.UtcNow.AddHours(2))
            .SetProperty(b => b.CreatedAt, DateTimeOffset.UtcNow.AddDays(-2))));

        await RunPassAsync();
        await RunPassAsync();

        using var scope = factory.Services.CreateScope();
        var mails = await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().EmailMessages
            .Where(m => m.CorrelationId == booking.Ref && m.Category == "payment-reminder")
            .ToListAsync();
        var mail = Assert.Single(mails);
        Assert.Contains(booking.Ref, mail.Subject);
        Assert.Contains("Balance due", mail.TextBody);
        // Passenger data stays out of the reminder.
        Assert.DoesNotContain("AB1234567", mail.TextBody);
        Assert.DoesNotContain("Petrov", mail.TextBody);
    }
}
