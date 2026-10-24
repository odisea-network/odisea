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
/// internals). Before an offer leaves the backend it MUST be wrapped in an
/// OfferEnvelope and protected with this service. When a client hands the token
/// back, the envelope is recovered together with the agency, hotel and occupancy
/// the offer was issued for — none of which the client can therefore forge.
public interface IOfferTokenProtector
{
    string Protect(OfferEnvelope envelope);

    /// <exception cref="InvalidOfferTokenException">Tampered or foreign token.</exception>
    OfferEnvelope Unprotect(string publicToken);
}

public sealed record OfferEnvelope(
    string ProviderToken,
    Guid AgencyId,
    Guid HotelId,
    int Adults,
    int Children);

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

/// Everything a reservation system may need to register a traveller. Booking
/// guarantees the required fields are present; adapters pick what their system uses.
public sealed record ProviderPassenger(
    string FirstName,
    string LastName,
    DateOnly BirthDate,
    ProviderPassengerType Type,
    ProviderGender Gender,
    string Nationality,
    ProviderTravelDocument? Document,
    bool IsLead,
    string? Title,
    string? Email,
    string? Phone);

public sealed record ProviderTravelDocument(
    ProviderDocumentType Type,
    string Number,
    string IssuingCountry,
    DateOnly ExpiryDate);

public enum ProviderPassengerType
{
    Adult,
    Child,
    Infant,
}

public enum ProviderGender
{
    Male,
    Female,
}

public enum ProviderDocumentType
{
    Passport,
    IdCard,
}

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
