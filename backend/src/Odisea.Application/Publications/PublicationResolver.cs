using Microsoft.EntityFrameworkCore;
using Odisea.Application.Common.Interfaces;
using Odisea.Application.Publications.Dtos;
using Odisea.Domain.Entities;

namespace Odisea.Application.Publications;

public static class PublicationResolver
{
    /// <summary>
    /// Builds the public-facing manifest for a Publication.
    /// The offersUrl is a relative path — callers combine it with their api-base.
    /// </summary>
    public static async Task<PublicationManifestDto> ResolveManifestAsync(
        Publication pub,
        IAppDbContext db,
        CancellationToken ct = default)
    {
        var collection = await db.Collections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == pub.CollectionId, ct)
            ?? throw new InvalidOperationException(
                $"Publication {pub.Key} references missing collection {pub.CollectionId}.");

        // Resolve offers by collection id: slugs are unique only per agency (#18), so
        // a slug-keyed public URL would be ambiguous across tenants. The id is global.
        var offersUrl = $"/api/v1/collections/{collection.Id}/offers";
        var etag = $"\"{pub.Version}-{pub.CollectionId}\"";

        // A referenced Experience entity is the source of truth for the manifest's
        // experience config; the inline ExperienceConfig is the legacy fallback for
        // publications created before the Experience entity existed.
        var experience = pub.ExperienceConfig;
        if (pub.ExperienceId is Guid experienceId)
        {
            var experienceEntity = await db.Experiences
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == experienceId, ct);
            if (experienceEntity is not null)
                experience = experienceEntity.Config;
        }

        return new PublicationManifestDto(
            Key: pub.Key,
            Version: pub.Version,
            Status: pub.Status.ToString(),
            CollectionId: pub.CollectionId,
            CollectionSlug: collection.Slug,
            CollectionName: collection.Name,
            OffersUrl: offersUrl,
            ThemeId: pub.ThemeId,
            Experience: experience,
            ETag: etag);
    }
}
