using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Booking.Domain;

namespace Odisea.Modules.Booking.Features.Bookings;

public record PassengerRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    DateOnly? BirthDate,
    bool IsLead = false);

public record CreateBookingRequest(
    [Required] Guid ProgramId,
    [Required] Guid DepartureId,
    [Required] Guid HotelId,
    [Required] string OfferToken,
    [Required, MinLength(1), MaxLength(9)] List<PassengerRequest> Passengers);

public record PassengerDto(Guid Id, string FirstName, string LastName, DateOnly? BirthDate, bool IsLead);

public record StatusHistoryDto(DateTimeOffset At, string From, string To, string? Note);

public record BookingDto(
    Guid Id,
    string Ref,
    string Status,
    string ProgramName,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    decimal? SellPrice,
    decimal? AgencyCommission,
    string Currency,
    string? ProviderBookingRef,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PassengerDto> Passengers,
    IReadOnlyList<StatusHistoryDto> History);

public record RePriceResponse(BookingDto Booking, bool PriceChanged, decimal? PreviousSellPrice);

public static class BookingDtoMapping
{
    public static BookingDto ToDto(this Domain.Booking b) => new(
        b.Id, b.Ref, b.Status.ToString(), b.ProgramName, b.HotelName,
        b.CheckIn, b.CheckOut, b.SellAmount, b.CommissionAmount, b.Currency,
        b.ProviderBookingRef, b.CreatedAt,
        [.. b.Passengers.OrderByDescending(p => p.IsLead).Select(p =>
            new PassengerDto(p.Id, p.FirstName, p.LastName, p.BirthDate, p.IsLead))],
        [.. b.History.OrderBy(h => h.CreatedAt).Select(h =>
            new StatusHistoryDto(h.CreatedAt, h.FromStatus, h.ToStatus, h.Note))]);
}
