using Odisea.Modules.Booking.Domain;
using Xunit;
using BookingEntity = Odisea.Modules.Booking.Domain.Booking;

namespace Odisea.UnitTests.Booking;

public class BookingStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private static BookingEntity NewBooking() => new()
    {
        Ref = "ODI-2027-000001",
        AgencyId = Guid.NewGuid(),
        AgentUserId = Guid.NewGuid(),
        ProgramId = Guid.NewGuid(),
        ProgramName = "P",
        DepartureId = Guid.NewGuid(),
        HotelId = Guid.NewGuid(),
        HotelName = "H",
        CheckIn = new DateOnly(2027, 3, 30),
        CheckOut = new DateOnly(2027, 4, 6),
        ProviderCode = "mock",
        OfferToken = "t",
    };

    [Theory]
    [InlineData(BookingStatus.PendingConfirmation)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Failed)]
    public void Draft_cannot_jump_ahead(BookingStatus target)
    {
        var booking = NewBooking();
        Assert.Throws<InvalidBookingTransitionException>(() => booking.TransitionTo(target, null, Now));
    }

    [Fact]
    public void Happy_path_walks_the_full_chain_and_records_history()
    {
        var booking = NewBooking();
        booking.TransitionTo(BookingStatus.PriceConfirmed, "priced", Now);
        booking.TransitionTo(BookingStatus.PriceConfirmed, "re-priced", Now); // allowed re-entry
        booking.TransitionTo(BookingStatus.PendingConfirmation, null, Now);
        booking.TransitionTo(BookingStatus.Confirmed, "MOCK-1", Now);
        booking.TransitionTo(BookingStatus.Cancelled, "customer changed mind", Now);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(5, booking.History.Count);
        Assert.Equal("Confirmed", booking.History[^1].FromStatus);
    }

    [Theory]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Failed)]
    public void Terminal_states_allow_nothing(BookingStatus terminal)
    {
        var booking = NewBooking();
        // walk legally into the terminal state
        if (terminal == BookingStatus.Cancelled)
        {
            booking.TransitionTo(BookingStatus.Cancelled, null, Now);
        }
        else
        {
            booking.TransitionTo(BookingStatus.PriceConfirmed, null, Now);
            booking.TransitionTo(BookingStatus.PendingConfirmation, null, Now);
            booking.TransitionTo(BookingStatus.Failed, null, Now);
        }

        foreach (var next in Enum.GetValues<BookingStatus>())
            Assert.Throws<InvalidBookingTransitionException>(() => booking.TransitionTo(next, null, Now));
    }
}
