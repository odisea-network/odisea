using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Features.Hotels;

public record HotelDto(Guid Id, string Name, Guid DestinationId, int Stars, DateTimeOffset CreatedAt);

public record CreateHotelRequest(
    [Required, MaxLength(200)] string Name,
    [Required] Guid DestinationId,
    [Range(1, 5)] int Stars);

public record CreateHotelMappingRequest(
    [Required, MaxLength(30)] string ProviderCode,
    [Required, MaxLength(100)] string ExternalHotelCode);

public record HotelMappingDto(Guid Id, Guid HotelId, string ProviderCode, string ExternalHotelCode);

public static class HotelDtoMapping
{
    public static HotelDto ToDto(this Hotel h) => new(h.Id, h.Name, h.DestinationId, h.Stars, h.CreatedAt);

    public static HotelMappingDto ToDto(this HotelMapping m) =>
        new(m.Id, m.HotelId, m.ProviderCode, m.ExternalHotelCode);
}
