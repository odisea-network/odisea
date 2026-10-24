namespace Odisea.Modules.Booking.Features.Bookings;

/// Payment deadline policy for reservations the provider is holding (appsettings "Booking").
public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    /// How long an unpaid reservation may be held after submit.
    public int PaymentOptionDays { get; set; } = 3;

    /// Payment must be in no later than this many days before check-in, whatever the option window.
    public int PaymentDueDaysBeforeCheckIn { get; set; } = 14;

    /// Never give the agency less than this to pay, even for last-minute departures.
    public int MinimumPaymentWindowHours { get; set; } = 24;
}

public static class PaymentDeadline
{
    /// The earlier of "now + option window" and "check-in − lead time (midnight UTC)",
    /// but never sooner than the minimum window.
    public static DateTimeOffset Compute(DateTimeOffset now, DateOnly checkIn, BookingOptions options)
    {
        var optionEnd = now.AddDays(options.PaymentOptionDays);
        var beforeCheckIn = new DateTimeOffset(
            checkIn.AddDays(-options.PaymentDueDaysBeforeCheckIn).ToDateTime(TimeOnly.MinValue),
            TimeSpan.Zero);

        var due = optionEnd < beforeCheckIn ? optionEnd : beforeCheckIn;
        var floor = now.AddHours(options.MinimumPaymentWindowHours);
        return due < floor ? floor : due;
    }
}
