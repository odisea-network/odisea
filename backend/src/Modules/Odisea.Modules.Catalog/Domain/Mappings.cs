using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

/// Internal hotel ↔ provider hotel code. Unique in both directions per provider.
public class HotelMapping : Entity
{
    public required Guid HotelId { get; set; }
    public required string ProviderCode { get; set; }
    public required string ExternalHotelCode { get; set; }
}

/// Internal destination ↔ provider location code. Unique in both directions per provider.
public class LocationMapping : Entity
{
    public required Guid DestinationId { get; set; }
    public required string ProviderCode { get; set; }
    public required string ExternalLocationCode { get; set; }
}

public sealed class MissingMappingException(string what)
    : DomainException($"Missing provider mapping: {what}.");
