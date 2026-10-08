using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Catalog.Domain;

namespace Odisea.Modules.Catalog.Features.Destinations;

public record DestinationDto(Guid Id, string Name, string Country, DateTimeOffset CreatedAt);

public record CreateDestinationRequest(
    [Required, MaxLength(200)] string Name,
    [Required, RegularExpression("^[A-Za-z]{2}$")] string Country);

public record CreateLocationMappingRequest(
    [Required, MaxLength(30)] string ProviderCode,
    [Required, MaxLength(100)] string ExternalLocationCode);

public record LocationMappingDto(Guid Id, Guid DestinationId, string ProviderCode, string ExternalLocationCode);

public static class DestinationDtoMapping
{
    public static DestinationDto ToDto(this Destination d) => new(d.Id, d.Name, d.Country, d.CreatedAt);

    public static LocationMappingDto ToDto(this LocationMapping m) =>
        new(m.Id, m.DestinationId, m.ProviderCode, m.ExternalLocationCode);
}
