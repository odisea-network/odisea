using Odisea.Domain.Common;

namespace Odisea.Domain.Entities;

public class Operator : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public List<SupplierConnection> Connections { get; set; } = new();
}
