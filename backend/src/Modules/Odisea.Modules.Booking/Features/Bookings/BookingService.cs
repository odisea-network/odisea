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

        // Clients hold protected tokens; the raw provider token stays internal.
        var providerToken = tokenProtector.Unprotect(request.OfferToken);

        var now = clock.UtcNow;
        var booking = new Domain.Booking
        {
            Ref = await refGenerator.NextRefAsync(target.CheckIn.Year, ct),
            AgencyId = agencyId,
            AgentUserId = agentUserId,
            ProgramId = target.ProgramId,
            ProgramName = target.ProgramName,
            DepartureId = target.DepartureId,
            HotelId = target.HotelId,
            HotelName = target.HotelName,
            CheckIn = target.CheckIn,
            CheckOut = target.CheckOut,
            ProviderCode = target.ProviderCode,
            OfferToken = providerToken,
        };
        booking.History.Add(new BookingStatusHistory
        {
            BookingId = booking.Id,
            FromStatus = "-",
            ToStatus = BookingStatus.Draft.ToString(),
            Note = "Draft created",
            CreatedAt = now,
        });

        var lead = request.Passengers.Any(p => p.IsLead) ? -1 : 0;
        for (var i = 0; i < request.Passengers.Count; i++)
        {
            var p = request.Passengers[i];
            booking.Passengers.Add(new BookingPassenger
            {
                BookingId = booking.Id,
                FirstName = p.FirstName,
                LastName = p.LastName,
                BirthDate = p.BirthDate,
                IsLead = p.IsLead || i == lead,
            });
        }

        db.Bookings.Add(booking);
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
        Transition(booking, BookingStatus.PendingConfirmation, "Sent to provider");

        var provider = providers.Get(booking.ProviderCode);
        var result = await provider.BookAsync(
            new ProviderBookingRequest(
                booking.OfferToken,
                [.. booking.Passengers.Select(p => new ProviderPassenger(p.FirstName, p.LastName, p.BirthDate))],
                booking.Ref),
            ct);

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
        ?? throw new BookingValidationException("Booking not found.");
}
