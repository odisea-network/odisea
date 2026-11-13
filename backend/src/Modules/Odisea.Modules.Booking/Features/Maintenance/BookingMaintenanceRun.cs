using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Notifications.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Features.Maintenance;

public sealed record BookingMaintenanceResult(int Expired, int ReleasesRetried, int RemindersQueued);

/// One pass of the booking maintenance job. Each booking is handled on its own, so one that
/// changed meanwhile (paid, cancelled, touched by an operator) is skipped instead of failing the pass.
public sealed class BookingMaintenanceRun(
    BookingDbContext db,
    OperatorBookingService operations,
    ProviderReleaseService releases,
    IAgencyLookup agencies,
    IEmailOutbox outbox,
    IClock clock,
    IOptions<BookingMaintenanceOptions> options,
    ILogger<BookingMaintenanceRun> logger)
{
    private static readonly TimeZoneInfo LocalZone = ResolveZone("Europe/Sofia", "FLE Standard Time");

    public async Task<BookingMaintenanceResult> RunAsync(CancellationToken ct)
    {
        var expired = await ExpireUnpaidAsync(ct);
        var retried = await RetryReleasesAsync(ct);
        var reminded = await RemindAsync(ct);
        if (expired + retried + reminded > 0)
            logger.LogInformation(
                "Booking maintenance: {Expired} expired, {Retried} supplier releases retried, {Reminded} reminders queued",
                expired, retried, reminded);
        return new BookingMaintenanceResult(expired, retried, reminded);
    }

    /// Wait before automatic release attempt n+1: doubles from two minutes, capped at six hours.
    public static TimeSpan ReleaseBackoff(int attempts) =>
        TimeSpan.FromMinutes(Math.Min(360, Math.Pow(2, Math.Max(1, attempts))));

    /// Holds whose deadline passed with nothing paid. A partly paid booking is the operator's call
    /// (expiring it means a refund), so it stays in the Today queue instead.
    private async Task<int> ExpireUnpaidAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.Bookings
            .Where(b => b.Status == BookingStatus.AwaitingPayment && b.PaymentDueAt != null && b.PaymentDueAt <= now)
            .Where(b => !b.Payments.Any(p => p.VoidedAt == null))
            .OrderBy(b => b.PaymentDueAt)
            .Select(b => b.Id)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        var done = 0;
        foreach (var id in due)
        {
            try
            {
                await operations.ExpireAsync(id, BookingActor.System, ct);
                done++;
            }
            catch (Exception ex) when (ex is DbUpdateConcurrencyException or BookingStateException
                or InvalidBookingTransitionException)
            {
                logger.LogInformation("Skipped expiring booking {BookingId}: it changed meanwhile", id);
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
        return done;
    }

    private async Task<int> RetryReleasesAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var candidates = await db.Bookings
            .Where(b => b.ProviderRelease == ProviderReleaseStatus.Pending
                && b.ProviderReleaseAttempts < options.Value.MaxReleaseAttempts)
            .OrderBy(b => b.ProviderReleaseUpdatedAt)
            .Select(b => new { b.Id, b.ProviderReleaseAttempts, b.ProviderReleaseUpdatedAt })
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        var done = 0;
        foreach (var c in candidates.Where(c =>
            c.ProviderReleaseUpdatedAt is not { } at || at + ReleaseBackoff(c.ProviderReleaseAttempts) <= now))
        {
            var booking = await db.Bookings.FirstAsync(b => b.Id == c.Id, ct);
            await releases.ReleaseAsync(booking, ct);
            db.ChangeTracker.Clear();
            done++;
        }
        return done;
    }

    /// One reminder per booking, to the agency's contact address, once the deadline is near.
    /// Bookings whose whole payment window is shorter than the lead time are not reminded: the
    /// deadline was in the confirmation they just received.
    private async Task<int> RemindAsync(CancellationToken ct)
    {
        var hours = options.Value.ReminderHoursBeforeDeadline;
        if (hours == 0)
            return 0;

        var now = clock.UtcNow;
        var horizon = now.AddHours(hours);
        var lead = TimeSpan.FromHours(hours);
        var bookings = await db.Bookings
            .Include(b => b.Payments)
            .Where(b => b.Status == BookingStatus.AwaitingPayment && b.PaymentReminderSentAt == null
                && b.PaymentDueAt != null && b.PaymentDueAt > now && b.PaymentDueAt <= horizon)
            .OrderBy(b => b.PaymentDueAt)
            .Take(options.Value.BatchSize)
            .ToListAsync(ct);

        var done = 0;
        foreach (var booking in bookings.Where(b => b.BalanceDue > 0 && b.CreatedAt <= b.PaymentDueAt!.Value - lead))
        {
            var agency = await agencies.GetContractInfoAsync(booking.AgencyId, ct);
            if (agency?.ContactEmail is not { Length: > 0 } email)
            {
                logger.LogInformation("No contact e-mail for agency {AgencyId}; booking {BookingId} not reminded",
                    booking.AgencyId, booking.Id);
                continue;
            }

            await outbox.EnqueueAsync(Reminder(booking, agency.Name, email), ct);
            booking.PaymentReminderSentAt = now;
            try
            {
                await db.SaveChangesAsync(ct);
                done++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // The booking changed meanwhile (paid, cancelled). The mail is already queued; if
                // the booking is still unpaid the next pass reminds again. A rare duplicate beats
                // a reminder that is never sent, which is the risk of marking before queueing.
                db.Entry(booking).State = EntityState.Detached;
            }
        }
        return done;
    }

    private static EmailRequest Reminder(Domain.Booking b, string agencyName, string email)
    {
        var bg = CultureInfo.GetCultureInfo("bg-BG");
        var dueLocal = TimeZoneInfo.ConvertTime(b.PaymentDueAt!.Value, LocalZone);
        var due = dueLocal.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
        var balance = b.BalanceDue.ToString("N2", bg);
        var stay = $"{b.CheckIn.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)} – {b.CheckOut.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
        var text =
            $"Hello {agencyName},\n\n" +
            $"Reservation {b.Ref} is still waiting for payment.\n\n" +
            $"  {b.ProgramName}, {b.HotelName}\n" +
            $"  {stay}, {b.Adults + b.Children} travellers\n" +
            $"  Balance due: {balance} {b.Currency}\n" +
            $"  Deadline: {due} (Sofia time)\n\n" +
            "If the payment is not recorded by then, the reservation expires and the supplier releases the rooms.\n" +
            "If you have already paid, please send the payment reference to the operator.\n";
        return new EmailRequest(
            To: email,
            Subject: $"Payment due by {due}: reservation {b.Ref}",
            TextBody: text,
            Category: "payment-reminder",
            AgencyId: b.AgencyId,
            CorrelationId: b.Ref,
            ToName: agencyName);
    }

    private static TimeZoneInfo ResolveZone(string ianaId, string windowsId)
    {
        foreach (var id in new[] { ianaId, windowsId })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }
        return TimeZoneInfo.Utc;
    }
}
