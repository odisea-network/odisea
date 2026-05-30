using Odisea.Domain.Common;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Domain.Entities;

public class Collection : Entity
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public CollectionStatus Status { get; set; } = CollectionStatus.Draft;

    public FilterSpec Filter { get; set; } = new();
    public List<ParameterDef> Parameters { get; set; } = new();

    public List<Guid> PinnedOfferIds { get; set; } = new();
    public List<Guid> ExcludedOfferIds { get; set; } = new();

    public SortSpec Sort { get; set; } = new("price", "asc");
}
