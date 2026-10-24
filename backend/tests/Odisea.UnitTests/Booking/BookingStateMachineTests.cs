using Odisea.Modules.Booking.Domain;
using Xunit;
using BookingEntity = Odisea.Modules.Booking.Domain.Booking;

namespace Odisea.UnitTests.Booking;

public class BookingStateMachineTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly BookingActor Agency = new(BookingActorType.Agency, Guid.NewGuid());
    private static readonly BookingActor Operator = new(BookingActorType.Operator, Guid.NewGuid());

    private static BookingEntity NewBooking(int adults = 2, int children = 0) => new()
    {
        Ref = "ODI-2027-000001",
        AgencyId = Guid.NewGuid(),
        AgentUserId = Guid.NewGuid(),
        Adults = adults,
        Children = children,
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

    private static BookingPassenger Complete(Guid bookingId, int position, DateOnly birthDate, bool lead = false) => new()
    {
        BookingId = bookingId,
        Position = position,
        FirstName = "Test",
        LastName = $"Passenger{position}",
        Gender = Gender.Female,
        BirthDate = birthDate,
        Nationality = "BG",
        DocumentType = TravelDocumentType.Passport,
        DocumentNumber = $"P{position}234567",
        DocumentIssuingCountry = "BG",
        DocumentExpiryDate = new DateOnly(2031, 1, 1),
        IsLead = lead,
        Email = lead ? "lead@example.com" : null,
        Phone = lead ? "+359888123456" : null,
    };

    private static BookingEntity ReadyBooking()
    {
        var booking = NewBooking();
        booking.SellAmount = 1000m;
        booking.Passengers.Add(Complete(booking.Id, 0, new DateOnly(1985, 1, 1), lead: true));
        booking.Passengers.Add(Complete(booking.Id, 1, new DateOnly(1988, 6, 6)));
        return booking;
    }

    private static void WalkToAwaitingPayment(BookingEntity booking)
    {
        booking.TransitionTo(BookingStatus.PriceConfirmed, Agency, "priced", Now);
        booking.TransitionTo(BookingStatus.PendingConfirmation, Agency, null, Now);
        booking.TransitionTo(BookingStatus.AwaitingPayment, BookingActor.System, "MOCK-1", Now);
    }

    [Theory]
    [InlineData(BookingStatus.PendingConfirmation)]
    [InlineData(BookingStatus.AwaitingPayment)]
    [InlineData(BookingStatus.Confirmed)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.Failed)]
    public void Draft_cannot_jump_ahead(BookingStatus target)
    {
        var booking = NewBooking();
        Assert.Throws<InvalidBookingTransitionException>(() => booking.TransitionTo(target, Agency, null, Now));
    }

    [Fact]
    public void Happy_path_walks_the_full_chain_and_records_who_did_what()
    {
        var booking = NewBooking();
        booking.TransitionTo(BookingStatus.PriceConfirmed, Agency, "priced", Now);
        booking.TransitionTo(BookingStatus.PriceConfirmed, Agency, "re-priced", Now); // allowed re-entry
        booking.TransitionTo(BookingStatus.PendingConfirmation, Agency, null, Now);
        booking.TransitionTo(BookingStatus.AwaitingPayment, BookingActor.System, "MOCK-1", Now);
        booking.TransitionTo(BookingStatus.Confirmed, Operator, "paid", Now);
        booking.TransitionTo(BookingStatus.Cancelled, Operator, "customer changed mind", Now);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(6, booking.History.Count);
        Assert.Equal("Confirmed", booking.History[^1].FromStatus);
        Assert.Equal(BookingActorType.Operator, booking.History[^1].ActorType);
        Assert.Equal(Operator.UserId, booking.History[^1].ActorUserId);
        Assert.Equal(BookingActorType.System, booking.History[3].ActorType);
        Assert.Null(booking.History[3].ActorUserId);
    }

    [Theory]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.Cancelled)]
    public void Awaiting_payment_can_end_without_confirmation(BookingStatus outcome)
    {
        var booking = NewBooking();
        WalkToAwaitingPayment(booking);
        booking.TransitionTo(outcome, Operator, null, Now);
        Assert.Equal(outcome, booking.Status);
    }

    [Fact]
    public void Confirmed_can_only_be_cancelled()
    {
        var booking = NewBooking();
        WalkToAwaitingPayment(booking);
        booking.TransitionTo(BookingStatus.Confirmed, Operator, null, Now);

        foreach (var next in Enum.GetValues<BookingStatus>().Where(s => s != BookingStatus.Cancelled))
            Assert.Throws<InvalidBookingTransitionException>(() => booking.TransitionTo(next, Operator, null, Now));
        booking.TransitionTo(BookingStatus.Cancelled, Operator, null, Now);
    }

    [Theory]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Failed)]
    public void Terminal_states_allow_nothing(BookingStatus terminal)
    {
        var booking = NewBooking();
        if (terminal == BookingStatus.Failed)
        {
            booking.TransitionTo(BookingStatus.PriceConfirmed, Agency, null, Now);
            booking.TransitionTo(BookingStatus.PendingConfirmation, Agency, null, Now);
            booking.TransitionTo(BookingStatus.Failed, BookingActor.System, null, Now);
        }
        else
        {
            WalkToAwaitingPayment(booking);
            booking.TransitionTo(terminal, Operator, null, Now);
        }

        foreach (var next in Enum.GetValues<BookingStatus>())
            Assert.Throws<InvalidBookingTransitionException>(() => booking.TransitionTo(next, Operator, null, Now));
    }

    [Fact]
    public void Complete_booking_is_ready_for_submit()
    {
        var booking = ReadyBooking();
        booking.EnsureReadyForSubmit();
    }

    [Fact]
    public void Missing_data_is_reported_by_position_never_by_name_or_document()
    {
        var booking = ReadyBooking();
        var second = booking.Passengers[1];
        second.FirstName = "Nameless";
        second.Nationality = null;
        second.DocumentNumber = null;

        var ex = Assert.Throws<BookingValidationException>(booking.EnsureReadyForSubmit);

        Assert.Contains("Passenger 2: missing nationality, document number.", ex.Message);
        Assert.DoesNotContain("Nameless", ex.Message);
        Assert.DoesNotContain("P1234567", ex.Message);
        Assert.DoesNotContain("Passenger 1", ex.Message);
    }

    [Fact]
    public void Lead_needs_contact_details()
    {
        var booking = ReadyBooking();
        booking.Passengers[0].Email = null;
        booking.Passengers[0].Phone = " ";

        var ex = Assert.Throws<BookingValidationException>(booking.EnsureReadyForSubmit);
        Assert.Contains("email (lead), phone (lead)", ex.Message);
    }

    [Fact]
    public void Party_size_must_match_the_priced_occupancy()
    {
        var booking = ReadyBooking();
        booking.Passengers.Add(Complete(booking.Id, 2, new DateOnly(1990, 1, 1)));

        var ex = Assert.Throws<BookingValidationException>(booking.EnsureReadyForSubmit);
        Assert.Contains("3 passenger(s) for an occupancy of 2 adult(s) and 0 child(ren)", ex.Message);
    }

    [Fact]
    public void Age_mix_must_match_the_priced_occupancy()
    {
        var booking = ReadyBooking();
        booking.Passengers[1].BirthDate = new DateOnly(2020, 1, 1); // a child priced as an adult

        var ex = Assert.Throws<BookingValidationException>(booking.EnsureReadyForSubmit);
        Assert.Contains("1 adult(s) and 1 child(ren), but the offer is for 2 adult(s) and 0 child(ren)", ex.Message);
    }

    [Fact]
    public void Documents_must_outlive_the_trip_and_exactly_one_passenger_leads()
    {
        var booking = ReadyBooking();
        booking.Passengers[1].DocumentExpiryDate = new DateOnly(2027, 4, 1);
        booking.Passengers[1].IsLead = true;

        var ex = Assert.Throws<BookingValidationException>(booking.EnsureReadyForSubmit);
        Assert.Contains("More than one lead passenger.", ex.Message);
        Assert.Contains("Passenger 2: travel document expires before check-out.", ex.Message);
    }

    [Fact]
    public void Unpriced_booking_cannot_be_submitted()
    {
        var booking = ReadyBooking();
        booking.SellAmount = null;

        var ex = Assert.Throws<BookingValidationException>(booking.EnsureReadyForSubmit);
        Assert.Contains("price has not been confirmed", ex.Message);
    }

    [Fact]
    public void Voided_payments_do_not_count_towards_the_balance()
    {
        var booking = ReadyBooking();
        var by = Guid.NewGuid();
        booking.Payments.Add(new BookingPayment
        {
            BookingId = booking.Id, Amount = 600m, Currency = "EUR", Method = PaymentMethod.BankTransfer,
            ReceivedOn = new DateOnly(2026, 10, 9), RecordedByUserId = by,
        });
        booking.Payments.Add(new BookingPayment
        {
            BookingId = booking.Id, Amount = 400m, Currency = "EUR", Method = PaymentMethod.Card,
            ReceivedOn = new DateOnly(2026, 10, 9), RecordedByUserId = by, VoidedAt = Now, VoidReason = "typo",
        });

        Assert.Equal(600m, booking.PaidAmount);
        Assert.Equal(400m, booking.BalanceDue);
        Assert.False(booking.IsFullyPaid);
    }
}
