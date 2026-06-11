using Odisea.Domain.Entities;

namespace Odisea.Application.Publications.Dtos;

public record AllowedDomainDto(
    Guid Id,
    Guid PublicationId,
    string Domain,
    DateTime CreatedAt
);

/// <summary>
/// Replaces a Publication's allowed-domain list wholesale. An empty list opens
/// the publication to any origin (the embed middleware logs a warning).
/// </summary>
public record ManageDomainsRequest(string[] Domains);

public static class AllowedDomainMappings
{
    public static AllowedDomainDto ToDto(this AllowedDomain d) =>
        new(d.Id, d.PublicationId, d.Domain, d.CreatedAt);
}
