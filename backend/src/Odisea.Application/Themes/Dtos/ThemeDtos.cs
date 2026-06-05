using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Themes.Dtos;

public record ThemeDto(
    Guid Id,
    Guid AgencyId,
    string Name,
    string Status,
    int Version,
    ThemeTokens Tokens
);

public record CreateThemeRequest(
    Guid AgencyId,
    string Name,
    ThemeTokens? Tokens
);

public record UpdateThemeRequest(
    string? Name,
    ThemeTokens? Tokens
);

public static class ThemeMappings
{
    public static ThemeDto ToDto(this Theme t) =>
        new(t.Id, t.AgencyId, t.Name, t.Status.ToString(), t.Version, t.Tokens);
}
