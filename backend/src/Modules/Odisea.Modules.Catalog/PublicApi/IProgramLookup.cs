namespace Odisea.Modules.Catalog.PublicApi;

/// Resolves a bookable (program, departure, hotel) combination for other modules.
public interface IProgramLookup
{
    /// Null when the program is not published, or the departure/hotel
    /// does not belong to it.
    Task<BookingTarget?> ResolveBookingTargetAsync(
        Guid programId, Guid departureId, Guid hotelId, CancellationToken ct);
}

public sealed record BookingTarget(
    Guid ProgramId,
    string ProgramName,
    string ProviderCode,
    Guid DepartureId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    Guid HotelId,
    string HotelName);
