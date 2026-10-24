using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.PublicApi;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Infrastructure;
using Odisea.Modules.Catalog.PublicApi;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.Modules.Pricing.PublicApi;
using Odisea.SharedKernel;

namespace Odisea.Modules.Booking.Features.Bookings;

public class BookingService(
    BookingDbContext db,
    IProviderRegistry providers,
    IOfferTokenProtector tokenProtector,
    IPriceCalculator priceCalculator,
    IAgencyLookup agencyLookup,
    IProgramLookup programLookup,
    IBookingRefGenerator refGenerator,
    IClock clock)
{
    public async Task<BookingDto> CreateDraftAsync(
        CreateBookingRequest request, Guid agencyId, Guid agentUserId, CancellationToken ct)
    {
        var agency = await agencyLookup.FindAsync(agencyId, ct);
        if (agency is null || !agency.IsActive)
            throw new BookingValidationException("Your agency is not active.");

        var target = await programLookup.ResolveBookingTargetAsync(
                request.ProgramId, request.DepartureId, request.HotelId, ct)
            ?? throw new BookingValidationException(
                "This program/departure/hotel combination is not bookable.");

        // Clients hold protected envelopes; the raw provider token stays internal. The envelope
        // pins the agency, hotel and occupancy the offer was issued for — none of it is trusted
        // from the request.
        var envelope = tokenProtector.Unprotect(request.OfferToken);
        if (envelope.AgencyId != agencyId || envelope.HotelId != target.HotelId)
            throw new InvalidOfferTokenException(target.ProviderCode);

        var now = clock.UtcNow;
        var booking = new Domain.Booking
        {
            Ref = await refGenerator.NextRefAsync(target.CheckIn.Year, ct),
            AgencyId = agencyId,
            AgentUserId = agentUserId,
            Adults = envelope.Adults,
            Children = envelope.Children,
            ProgramId = target.ProgramId,
            ProgramName = target.ProgramName,
            DepartureId = target.DepartureId,
            HotelId = target.HotelId,
            HotelName = target.HotelName,
            CheckIn = target.CheckIn,
            CheckOut = target.CheckOut,
            ProviderCode = target.ProviderCode,
            OfferToken = envelope.ProviderToken,
        };
        booking.History.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            FromStatus = "-",
            ToStatus = BookingStatus.Draft.ToString(),
            Note = "Draft created",
            CreatedAt = now,
        });

        AddPassengers(booking, request.Passengers);

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);
        return booking.ToDto();
    }

    public async Task<BookingDto> ReplacePassengersAsync(
        Guid bookingId, Guid agencyId, UpdatePassengersRequest request, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(bookingId, agencyId, ct);
        if (booking.Status is not (BookingStatus.Draft or BookingStatus.PriceConfirmed))
            throw new BookingStateException(booking.Status, "change the passengers of");

        // Replace wholesale: passenger identity is not stable across edits, and a merge would
        // need client-supplied passenger ids that we would then have to trust.
        db.BookingPassengers.RemoveRange(booking.Passengers);
        booking.Passengers.Clear();
        AddPassengers(booking, request.Passengers);

        await db.SaveChangesAsync(ct);
        return booking.ToDto();
    }

    public async Task<RePriceResponse> RePriceAsync(Guid bookingId, Guid agencyId, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(bookingId, agencyId, ct);
        if (booking.Status is not (BookingStatus.Draft or BookingStatus.PriceConfirmed))
            throw new InvalidBookingTransitionException(booking.Status, BookingStatus.PriceConfirmed);

        var provider = providers.Get(booking.ProviderCode);
        var result = await provider.RePriceAsync(booking.OfferToken, ct);
        if (!result.IsAvailable || result.CurrentNetPrice is not { } net)
            throw new OfferUnavailableException();

        var breakdown = await priceCalculator.CalculateSellPriceAsync(
            new PricingContext(net, booking.ProgramId, booking.AgencyId, booking.CheckIn), ct);

        var previousSell = booking.SellAmount;
        var priceChanged = previousSell is { } prev && prev != breakdown.SellPrice.Amount;

        booking.OfferToken = result.NewOfferToken ?? booking.OfferToken;
        booking.NetAmount = net.Amount;
        booking.SellAmount = breakdown.SellPrice.Amount;
        booking.CommissionAmount = breakdown.AgencyCommission.Amount;
        booking.Currency = breakdown.SellPrice.Currency;
        booking.BreakdownJson = JsonSerializer.Serialize(breakdown);
        Transition(booking, BookingStatus.PriceConfirmed,
            priceChanged
                ? $"Re-priced: {breakdown.SellPrice} (was {previousSell} {booking.Currency})"
                : $"Price confirmed: {breakdown.SellPrice}");

        await db.SaveChangesAsync(ct);
        return new RePriceResponse(booking.ToDto(), priceChanged, previousSell);
    }

    public async Task<BookingDto> ConfirmAsync(Guid bookingId, Guid agencyId, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(bookingId, agencyId, ct);
        var passengers = booking.Passengers
            .OrderBy(p => p.Position)
            .Select(p => ToProviderPassenger(p, booking.CheckIn))
            .ToList();
        Transition(booking, BookingStatus.PendingConfirmation, "Sent to provider");

        var provider = providers.Get(booking.ProviderCode);
        var result = await provider.BookAsync(
            new ProviderBookingRequest(booking.OfferToken, passengers, booking.Ref), ct);

        if (result.Success)
        {
            booking.ProviderBookingRef = result.ProviderBookingRef;
            Transition(booking, BookingStatus.Confirmed, $"Provider confirmed: {result.ProviderBookingRef}");
        }
        else
        {
            Transition(booking, BookingStatus.Failed, result.FailureReason);
        }

        await db.SaveChangesAsync(ct);
        return booking.ToDto();
    }

    public async Task<BookingDto> CancelAsync(Guid bookingId, Guid agencyId, CancellationToken ct)
    {
        var booking = await LoadOwnedAsync(bookingId, agencyId, ct);

        if (booking.Status == BookingStatus.Confirmed)
        {
            var provider = providers.Get(booking.ProviderCode);
            var result = await provider.CancelAsync(booking.ProviderBookingRef!, ct);
            if (!result.Success)
                throw new BookingValidationException(
                    $"The provider refused the cancellation: {result.FailureReason}");
        }

        Transition(booking, BookingStatus.Cancelled, "Cancelled by agency");
        await db.SaveChangesAsync(ct);
        return booking.ToDto();
    }

    public async Task<BookingDto?> GetAsync(Guid bookingId, Guid agencyId, CancellationToken ct)
    {
        var booking = await db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.History)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.AgencyId == agencyId, ct);
        return booking?.ToDto();
    }

    public async Task<List<BookingDto>> ListAsync(Guid agencyId, CancellationToken ct) =>
        [.. (await db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.History)
            .AsNoTracking()
            .Where(b => b.AgencyId == agencyId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct))
            .Select(b => b.ToDto())];

    /// Exactly one lead: the flagged passenger, or the first one when nobody is flagged.
    /// Country codes are normalised to upper case; the document number keeps the case of
    /// the document but is never logged anywhere.
    private void AddPassengers(Domain.Booking booking, IReadOnlyList<PassengerRequest> passengers)
    {
        var leads = passengers.Count(p => p.IsLead);
        if (leads > 1)
            throw new BookingValidationException("Only one passenger can be the lead.");

        for (var i = 0; i < passengers.Count; i++)
        {
            var p = passengers[i];
            var passenger = new BookingPassenger
            {
                BookingId = booking.Id,
                Position = i,
                Title = p.Title?.Trim(),
                Gender = p.Gender,
                FirstName = p.FirstName.Trim(),
                LastName = p.LastName.Trim(),
                BirthDate = p.BirthDate,
                Nationality = p.Nationality?.ToUpperInvariant(),
                DocumentType = p.DocumentType,
                DocumentNumber = p.DocumentNumber?.ToUpperInvariant(),
                DocumentIssuingCountry = p.DocumentIssuingCountry?.ToUpperInvariant(),
                DocumentExpiryDate = p.DocumentExpiryDate,
                IsLead = p.IsLead || (leads == 0 && i == 0),
                Email = p.Email?.Trim(),
                Phone = p.Phone?.Trim(),
            };
            booking.Passengers.Add(passenger);
            // Preset v7 ids would make change-tracker discovery treat the row as Modified.
            db.Add(passenger);
        }
    }

    /// The provider contract carries what real systems register travellers with. Missing
    /// required data is a validation error at this point; the submit flow checks the full
    /// completeness rules up front.
    private static ProviderPassenger ToProviderPassenger(BookingPassenger p, DateOnly checkIn)
    {
        if (p.BirthDate is not { } birthDate || p.Gender is not { } gender || p.Nationality is null)
            throw new BookingValidationException(
                $"Passenger {p.Position + 1}: birth date, gender and nationality are required.");

        ProviderTravelDocument? document = null;
        if (p.DocumentType is { } documentType
            && p.DocumentNumber is not null
            && p.DocumentIssuingCountry is not null
            && p.DocumentExpiryDate is { } expiry)
        {
            document = new ProviderTravelDocument(
                ToProvider(documentType), p.DocumentNumber, p.DocumentIssuingCountry, expiry);
        }

        return new ProviderPassenger(
            p.FirstName, p.LastName, birthDate,
            ToProvider(BookingPassenger.DeriveType(birthDate, checkIn)),
            ToProvider(gender), p.Nationality, document, p.IsLead, p.Title, p.Email, p.Phone);
    }

    // Booking keeps its own enums so the persisted domain never couples to the provider contract.
    private static ProviderPassengerType ToProvider(PassengerType type) => type switch
    {
        PassengerType.Adult => ProviderPassengerType.Adult,
        PassengerType.Child => ProviderPassengerType.Child,
        PassengerType.Infant => ProviderPassengerType.Infant,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    private static ProviderGender ToProvider(Gender gender) => gender switch
    {
        Gender.Male => ProviderGender.Male,
        Gender.Female => ProviderGender.Female,
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };

    private static ProviderDocumentType ToProvider(TravelDocumentType type) => type switch
    {
        TravelDocumentType.Passport => ProviderDocumentType.Passport,
        TravelDocumentType.IdCard => ProviderDocumentType.IdCard,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    /// TransitionTo creates the history row inside the domain entity. Its id is
    /// preset (v7 GUID), so change-tracker discovery would classify it as an
    /// EXISTING row (Modified) — explicitly Add it so EF inserts instead.
    private void Transition(Domain.Booking booking, BookingStatus next, string? note)
    {
        booking.TransitionTo(next, note, clock.UtcNow);
        db.Add(booking.History[^1]);
    }

    private async Task<Domain.Booking> LoadOwnedAsync(Guid bookingId, Guid agencyId, CancellationToken ct) =>
        await db.Bookings
            .Include(b => b.Passengers)
            .Include(b => b.History)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.AgencyId == agencyId, ct)
        ?? throw new BookingNotFoundException();
}
