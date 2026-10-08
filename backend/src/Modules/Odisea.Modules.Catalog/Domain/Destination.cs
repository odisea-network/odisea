using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

public class Destination : Entity
{
    public required string Name { get; set; }

    /// ISO 3166-1 alpha-2, e.g. "TR".
    public required string Country { get; set; }
}
