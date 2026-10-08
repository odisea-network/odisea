using System.ComponentModel.DataAnnotations;

namespace Odisea.Modules.Catalog.Features.Search;

public record SearchRequest(
    [Required] Guid ProgramId,
    [Required] Guid DepartureId,
    [Range(1, 9)] int Adults,
    [Range(0, 9)] int Children);

/// Agency-facing: sell price and commission only. Net cost NEVER leaves the backend.
public record HotelOfferDto(
    Guid HotelId,
    string HotelName,
    int Stars,
    string RoomType,
    string Board,
    decimal SellPrice,
    string Currency,
    decimal AgencyCommission,
    string OfferToken,
    DateTimeOffset? OfferExpiresAt);

public record SearchResponse(
    Guid ProgramId,
    string ProgramName,
    Guid DepartureId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    IReadOnlyList<HotelOfferDto> Offers);
