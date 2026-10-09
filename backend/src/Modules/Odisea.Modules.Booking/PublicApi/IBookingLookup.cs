namespace Odisea.Modules.Booking.PublicApi;

/// Read-only booking facts for invoicing. The tax event for a confirmed
/// booking is the provider confirmation; for a cancelled one, the cancellation.
public interface IBookingLookup
{
    Task<BookingFinancialInfo?> FindAsync(Guid bookingId, CancellationToken ct);
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
