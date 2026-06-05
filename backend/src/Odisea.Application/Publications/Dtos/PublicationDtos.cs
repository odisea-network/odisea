using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Publications.Dtos;

/// <summary>
/// The public-facing manifest returned by GET /api/v1/publications/{key}.
/// Cacheable via ETag; clients use offersUrl to fetch resolved offers.
/// </summary>
public record PublicationManifestDto(
    string Key,
    int Version,
    string Status,
    Guid CollectionId,
    string CollectionSlug,
    string CollectionName,
    string OffersUrl,
    Guid? ThemeId,
    ExperienceConfig? Experience,
    string ETag
);

public record PublicationDto(
    Guid Id,
    Guid AgencyId,
    string Key,
    Guid CollectionId,
    Guid? ThemeId,
    Guid? ExperienceId,
    ExperienceConfig? ExperienceConfig,
    string Status,
    string[] AllowedDomains,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreatePublicationRequest(
    Guid AgencyId,
    Guid CollectionId,
    Guid? ThemeId,
    Guid? ExperienceId,
    ExperienceConfig? ExperienceConfig,
    string[]? AllowedDomains
);

public record UpdatePublicationRequest(
    Guid? CollectionId,
    Guid? ThemeId,
    Guid? ExperienceId,
    ExperienceConfig? ExperienceConfig,
    string[]? AllowedDomains
);

public static class PublicationMappings
{
    public static PublicationDto ToDto(this Publication p) => new(
        p.Id, p.AgencyId, p.Key, p.CollectionId, p.ThemeId, p.ExperienceId,
        p.ExperienceConfig, p.Status.ToString(), p.AllowedDomains,
        p.Version, p.CreatedAt, p.UpdatedAt);
}
