using Odisea.SharedKernel;

namespace Odisea.Modules.Integrations.PublicApi;

/// The provider-neutral contract every external reservation system implements.
/// Offer tokens are OPAQUE: only the adapter that issued one may parse it.
public interface IReservationProvider
{
    string ProviderCode { get; }
    Task<ProviderSearchResult> SearchAsync(ProviderSearchRequest request, CancellationToken ct);
    Task<ProviderRePriceResult> RePriceAsync(string offerToken, CancellationToken ct);
    Task<ProviderBookingResult> BookAsync(ProviderBookingRequest request, CancellationToken ct);
    Task<ProviderCancelResult> CancelAsync(string providerBookingRef, CancellationToken ct);
    Task<ProviderBookingState> GetBookingStateAsync(string providerBookingRef, CancellationToken ct);
}

public interface IProviderRegistry
{
    IReservationProvider Get(string providerCode);
    IReadOnlyList<string> KnownProviderCodes { get; }
}

/// Provider offer tokens may embed sensitive state (net cost, availability
/// internals). Before a token leaves the backend it MUST be protected with
/// this service, and unprotected again when a client hands it back.
public interface IOfferTokenProtector
{
    string Protect(string providerToken);

    /// <exception cref="InvalidOfferTokenException">Tampered or foreign token.</exception>
    string Unprotect(string publicToken);
}

public sealed record ProviderSearchRequest(
    string ExternalLocationCode,
    DateRange Stay,
    Pax Pax,
    IReadOnlyList<string> ExternalHotelCodes);

public sealed record ProviderOffer(
    string OfferToken,
    string ExternalHotelCode,
    string RoomType,
    string Board,
    Money NetPrice,
    DateTimeOffset? OfferExpiresAt);

public sealed record ProviderSearchResult(IReadOnlyList<ProviderOffer> Offers);

public sealed record ProviderRePriceResult(
    bool IsAvailable,
    Money? CurrentNetPrice,
    string? NewOfferToken);

public sealed record ProviderPassenger(string FirstName, string LastName, DateOnly? BirthDate);

public sealed record ProviderBookingRequest(
    string OfferToken,
    IReadOnlyList<ProviderPassenger> Passengers,
    string ClientReference);

public sealed record ProviderBookingResult(
    bool Success,
    string? ProviderBookingRef,
    string? FailureReason);

public sealed record ProviderCancelResult(bool Success, string? FailureReason);

public enum ProviderBookingState
{
    Unknown,
    Pending,
    Confirmed,
    Cancelled,
}

public sealed class UnknownProviderException(string providerCode)
    : DomainException($"No reservation provider is registered for code '{providerCode}'.");

public sealed class InvalidOfferTokenException(string providerCode)
    : DomainException($"The offer token is not valid for provider '{providerCode}'.");
