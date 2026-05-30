using Odisea.Domain.Common;

namespace Odisea.Domain.Entities;

public class Agency : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}
