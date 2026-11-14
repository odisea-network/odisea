using System.ComponentModel.DataAnnotations;

namespace Odisea.Modules.Booking.Features.Maintenance;

/// Section "Booking:Maintenance": the background job that expires unpaid holds, retries supplier
/// releases and reminds agencies before a payment deadline.
public sealed class BookingMaintenanceOptions
{
    public const string SectionName = "Booking:Maintenance";

    public bool Enabled { get; set; } = true;

    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 5;

    /// Bookings handled per step and pass; the rest wait for the next pass.
    [Range(1, 500)]
    public int BatchSize { get; set; } = 50;

    /// Automatic release retries before the job leaves a pending release to an operator.
    [Range(1, 50)]
    public int MaxReleaseAttempts { get; set; } = 8;

    /// How long before the payment deadline the agency is reminded; 0 turns reminders off.
    [Range(0, 168)]
    public int ReminderHoursBeforeDeadline { get; set; } = 24;
}
