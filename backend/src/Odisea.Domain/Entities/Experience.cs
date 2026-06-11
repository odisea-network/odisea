using Odisea.Domain.Common;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Domain.Entities;

public class Experience : Entity
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ExperienceStatus Status { get; set; } = ExperienceStatus.Draft;
    public int Version { get; set; } = 1;
    public ExperienceConfig Config { get; set; } = new();
}
