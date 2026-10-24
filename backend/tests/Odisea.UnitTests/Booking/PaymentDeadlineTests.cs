using Odisea.Modules.Booking.Features.Bookings;
using Xunit;

namespace Odisea.UnitTests.Booking;

public class PaymentDeadlineTests
{
    private static readonly BookingOptions Options = new(); // 3 days option, 14 days before check-in, 24h floor
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Far_departures_get_the_option_window() =>
        Assert.Equal(Now.AddDays(3), PaymentDeadline.Compute(Now, new DateOnly(2027, 3, 30), Options));

    [Fact]
    public void Near_departures_are_capped_at_the_lead_time_before_check_in() =>
        Assert.Equal(
            new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero), // check-in − 14 days, before the 3-day option ends
            PaymentDeadline.Compute(Now, new DateOnly(2026, 10, 26), Options));

    [Fact]
    public void The_minimum_window_wins_over_a_lead_time_that_is_already_too_close() =>
        Assert.Equal(Now.AddHours(24), PaymentDeadline.Compute(Now, new DateOnly(2026, 10, 24), Options));

    [Fact]
    public void Last_minute_departures_still_get_the_minimum_window() =>
        Assert.Equal(Now.AddHours(24), PaymentDeadline.Compute(Now, new DateOnly(2026, 10, 12), Options));
}
