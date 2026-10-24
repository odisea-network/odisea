namespace Odisea.Modules.Booking.PublicApi;

/// Read-only booking facts for other modules. The tax event for a confirmed booking is the
/// operator's confirmation (given only once fully paid); for a cancelled one, the cancellation.
public interface IBookingLookup
{
    Task<BookingFinancialInfo?> FindAsync(Guid bookingId, CancellationToken ct);

    /// Everything a contract or voucher may print about a booking. Passenger document numbers
    /// are included because travel contracts identify the travellers by them; callers must
    /// treat the result as personal data (never log it).
    Task<BookingContractInfo?> GetContractInfoAsync(Guid bookingId, CancellationToken ct);
}

public sealed record BookingFinancialInfo(
    Guid Id,
    string Ref,
    Guid AgencyId,
    string Status,
    decimal? SellAmount,
    string Currency,
    string ProgramName,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt);

public sealed record BookingContractInfo(
    Guid Id,
    string Ref,
    Guid AgencyId,
    string Status,
    string ProgramName,
    string HotelName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    decimal? SellAmount,
    decimal? CommissionAmount,
    string Currency,
    decimal PaidAmount,
    decimal BalanceDue,
    DateTimeOffset? PaymentDueAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    IReadOnlyList<BookingPassengerInfo> Passengers);

public sealed record BookingPassengerInfo(
    int Position,
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
