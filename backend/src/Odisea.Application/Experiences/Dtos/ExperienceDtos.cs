using Odisea.Domain.Entities;
using Odisea.Domain.ValueObjects;

namespace Odisea.Application.Experiences.Dtos;

public record ExperienceDto(
    Guid Id,
    Guid AgencyId,
    string Name,
    string Status,
    int Version,
    ExperienceConfig Config
);

public record CreateExperienceRequest(
    Guid AgencyId,
    string Name,
    ExperienceConfig? Config
);

public record UpdateExperienceRequest(
    string? Name,
    ExperienceConfig? Config
);

public static class ExperienceMappings
{
    public static ExperienceDto ToDto(this Experience e) =>
        new(e.Id, e.AgencyId, e.Name, e.Status.ToString(), e.Version, e.Config);
}
