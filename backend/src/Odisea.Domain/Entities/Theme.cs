using Odisea.Domain.Common;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Domain.Entities;

public class Theme : Entity
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ThemeStatus Status { get; set; } = ThemeStatus.Draft;
    public int Version { get; set; } = 1;
    public ThemeTokens Tokens { get; set; } = ThemeTokens.Default();

    // Platform-owned template any agency can browse and clone (the theme
    // marketplace). Presets carry AgencyId Guid.Empty and never appear in an
    // agency's own theme list.
    public bool IsPreset { get; set; }
}
