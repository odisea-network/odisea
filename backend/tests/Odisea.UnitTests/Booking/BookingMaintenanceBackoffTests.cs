using Odisea.Modules.Booking.Features.Maintenance;
using Xunit;

namespace Odisea.UnitTests.Booking;

public class BookingMaintenanceBackoffTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(5, 32)]
    [InlineData(8, 256)]
    [InlineData(9, 360)]
    [InlineData(40, 360)]
    public void Release_retries_back_off_from_two_minutes_to_six_hours(int attempts, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), BookingMaintenanceRun.ReleaseBackoff(attempts));
}
