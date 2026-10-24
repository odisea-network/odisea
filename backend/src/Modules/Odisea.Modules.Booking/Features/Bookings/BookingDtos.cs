using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Booking.Domain;

namespace Odisea.Modules.Booking.Features.Bookings;

/// Input shapes for passenger data. Names as printed in the travel document: Latin letters
/// (diacritics allowed), spaces, hyphens and apostrophes. Country codes are ISO 3166-1 alpha-2.
public static class PassengerPatterns
{
    public const string LatinName = @"^[A-Za-zÀ-ɏ][A-Za-zÀ-ɏ '\-]*$";
    public const string CountryCode = "^[A-Za-z]{2}$";
    public const string DocumentNumber = "^[A-Za-z0-9]{1,20}$";
    public const string Phone = @"^\+?[0-9 ()\-]{6,30}$";
}

/// Everything beyond the names may be supplied later via PUT /passengers; submit checks completeness.
public record PassengerRequest(
    [Required, MaxLength(100), RegularExpression(PassengerPatterns.LatinName)] string FirstName,
    [Required, MaxLength(100), RegularExpression(PassengerPatterns.LatinName)] string LastName,
    DateOnly? BirthDate = null,
    [MaxLength(10)] string? Title = null,
    Gender? Gender = null,
    [RegularExpression(PassengerPatterns.CountryCode)] string? Nationality = null,
    TravelDocumentType? DocumentType = null,
    [RegularExpression(PassengerPatterns.DocumentNumber)] string? DocumentNumber = null,
    [RegularExpression(PassengerPatterns.CountryCode)] string? DocumentIssuingCountry = null,
    DateOnly? DocumentExpiryDate = null,
    bool IsLead = false,
    [EmailAddress, MaxLength(320)] string? Email = null,
    [RegularExpression(PassengerPatterns.Phone)] string? Phone = null);

public record CreateBookingRequest(
    [Required] Guid ProgramId,
    [Required] Guid DepartureId,
    [Required] Guid HotelId,
    [Required] string OfferToken,
    [Required, MinLength(1), MaxLength(9)] List<PassengerRequest> Passengers);

public record UpdatePassengersRequest(
    [Required, MinLength(1), MaxLength(9)] List<PassengerRequest> Passengers);

public record PassengerDto(
    Guid Id,
    string? Title,
    string? Gender,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? PassengerType,
    string? Nationality,
    string? DocumentType,
    string? DocumentNumber,
    string? DocumentIssuingCountry,
    DateOnly? DocumentExpiryDate,
    bool IsLead,
    string? Email,
    string? Phone);

/// Agencies see who acted (agency, API client, operator, system) but not which operator.
public record StatusHistoryDto(DateTimeOffset At, string From, string To, string? Note, string Actor);

public record BookingDto(
    Guid Id,
    string Ref,
    string Status,
    string ProgramName,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    decimal? SellPrice,
    decimal? AgencyCommission,
    string Currency,
    decimal PaidAmount,
    decimal BalanceDue,
    DateTimeOffset? PaymentDueAt,
    string? ProviderBookingRef,
    DateTimeOffset CreatedAt,
    IReadOnlyList<PassengerDto> Passengers,
    IReadOnlyList<StatusHistoryDto> History);

public record RePriceResponse(BookingDto Booking, bool PriceChanged, decimal? PreviousSellPrice);

public static class BookingDtoMapping
{
    public static BookingDto ToDto(this Domain.Booking b) => new(
        b.Id, b.Ref, b.Status.ToString(), b.ProgramName, b.HotelName,
        b.CheckIn, b.CheckOut, b.Adults, b.Children,
        b.SellAmount, b.CommissionAmount, b.Currency,
        b.PaidAmount, b.BalanceDue, b.PaymentDueAt,
        b.ProviderBookingRef, b.CreatedAt,
        [.. b.Passengers.OrderBy(p => p.Position).Select(p => p.ToDto(b.CheckIn))],
        [.. b.History.OrderBy(h => h.CreatedAt).Select(h =>
            new StatusHistoryDto(h.CreatedAt, h.FromStatus, h.ToStatus, h.Note, h.ActorType.ToString()))]);

    public static PassengerDto ToDto(this BookingPassenger p, DateOnly checkIn) => new(
        p.Id, p.Title, p.Gender?.ToString(), p.FirstName, p.LastName, p.BirthDate,
        p.TypeAt(checkIn)?.ToString(), p.Nationality, p.DocumentType?.ToString(), p.DocumentNumber,
        p.DocumentIssuingCountry, p.DocumentExpiryDate, p.IsLead, p.Email, p.Phone);
}
