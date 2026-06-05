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

        var offersUrl = $"/api/v1/collections/{collection.Slug}/offers";
        var etag = $"\"{pub.Version}-{pub.CollectionId}\"";

        return new PublicationManifestDto(
            Key: pub.Key,
            Version: pub.Version,
            Status: pub.Status.ToString(),
            CollectionId: pub.CollectionId,
            CollectionSlug: collection.Slug,
            CollectionName: collection.Name,
            OffersUrl: offersUrl,
            ThemeId: pub.ThemeId,
            Experience: pub.ExperienceConfig,
            ETag: etag);
    }
}
