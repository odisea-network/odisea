using Odisea.Modules.Booking.Domain;
using Xunit;

namespace Odisea.UnitTests.Booking;

public class PassengerTypeTests
{
    private static readonly DateOnly CheckIn = new(2027, 3, 30);

    [Theory]
    [InlineData(2025, 3, 31, PassengerType.Infant)] // turns 2 the day after check-in
    [InlineData(2025, 3, 30, PassengerType.Child)]  // turns 2 on check-in day
    [InlineData(2015, 3, 31, PassengerType.Child)]  // turns 12 the day after check-in
    [InlineData(2015, 3, 30, PassengerType.Adult)]  // turns 12 on check-in day
    [InlineData(2027, 1, 15, PassengerType.Infant)]
    [InlineData(1990, 5, 1, PassengerType.Adult)]
    public void Type_follows_completed_years_at_check_in(int year, int month, int day, PassengerType expected) =>
        Assert.Equal(expected, BookingPassenger.DeriveType(new DateOnly(year, month, day), CheckIn));

    [Fact]
    public void Without_a_birth_date_the_type_is_unknown()
    {
        var passenger = new BookingPassenger { BookingId = Guid.NewGuid(), Position = 0, FirstName = "Ana", LastName = "Ivanova" };
        Assert.Null(passenger.TypeAt(CheckIn));
    }
}
