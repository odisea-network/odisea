using Odisea.SharedKernel;

namespace Odisea.Modules.Catalog.Domain;

public class Hotel : Entity
{
    public required string Name { get; set; }
    public required Guid DestinationId { get; set; }
    public int Stars { get; set; }
}
