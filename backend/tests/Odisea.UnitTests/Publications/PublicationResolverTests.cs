using Microsoft.EntityFrameworkCore;
using Odisea.Application.Publications;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Publications;

public class PublicationResolverTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ResolveManifest_ReturnsCorrectShape()
    {
        await using var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Summer in Greece",
            Slug = "summer-greece",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var pub = new Publication
        {
            Key = "abc123def456",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            Status = PublicationStatus.Published,
            Version = 2,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var manifest = await PublicationResolver.ResolveManifestAsync(pub, db);

        Assert.Equal("abc123def456", manifest.Key);
        Assert.Equal(2, manifest.Version);
        Assert.Equal("Published", manifest.Status);
        Assert.Equal(collection.Id, manifest.CollectionId);
        Assert.Equal("summer-greece", manifest.CollectionSlug);
        Assert.Equal("Summer in Greece", manifest.CollectionName);
        Assert.Equal($"/api/v1/collections/{collection.Id}/offers", manifest.OffersUrl);
        Assert.Null(manifest.ThemeId);
        Assert.Null(manifest.Experience);
    }

    [Fact]
    public async Task ResolveManifest_ETagContainsVersionAndCollectionId()
    {
        await using var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Test",
            Slug = "test-col",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var pub = new Publication
        {
            Key = "etag001",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            Version = 5,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var manifest = await PublicationResolver.ResolveManifestAsync(pub, db);

        Assert.Equal($"\"5-{collection.Id}\"", manifest.ETag);
    }

    [Fact]
    public async Task ResolveManifest_PropagatesToThemeId()
    {
        await using var db = CreateDb();

        var themeId = Guid.NewGuid();
        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Themed",
            Slug = "themed-col",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var pub = new Publication
        {
            Key = "themed001",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            ThemeId = themeId,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var manifest = await PublicationResolver.ResolveManifestAsync(pub, db);

        Assert.Equal(themeId, manifest.ThemeId);
    }

    [Fact]
    public async Task ResolveManifest_PropagatesExperienceConfig()
    {
        await using var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Experience",
            Slug = "exp-col",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var exp = new ExperienceConfig(Type: "carousel", Columns: 2, CardStyle: "compact",
            ShowPrice: false, Inquiry: true, OpenNewTab: true);

        var pub = new Publication
        {
            Key = "exp001",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            ExperienceConfig = exp,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var manifest = await PublicationResolver.ResolveManifestAsync(pub, db);

        Assert.NotNull(manifest.Experience);
        Assert.Equal("carousel", manifest.Experience.Type);
        Assert.Equal(2, manifest.Experience.Columns);
        Assert.False(manifest.Experience.ShowPrice);
        Assert.True(manifest.Experience.OpenNewTab);
    }

    [Fact]
    public async Task ResolveManifest_AttachedExperience_FlowsIntoManifest()
    {
        await using var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Linked Experience",
            Slug = "linked-exp",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var experience = new Experience
        {
            AgencyId = collection.AgencyId,
            Name = "Carousel",
            Status = ExperienceStatus.Published,
            Config = new ExperienceConfig(Type: "carousel", Columns: 4, CardStyle: "editorial",
                ShowPrice: false, Inquiry: false, OpenNewTab: true),
        };
        db.Experiences.Add(experience);

        // Inline config is intentionally different to prove the entity takes precedence.
        var pub = new Publication
        {
            Key = "linkedexp001",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
            ExperienceId = experience.Id,
            ExperienceConfig = new ExperienceConfig(Type: "grid"),
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var manifest = await PublicationResolver.ResolveManifestAsync(pub, db);

        Assert.NotNull(manifest.Experience);
        Assert.Equal("carousel", manifest.Experience.Type);
        Assert.Equal(4, manifest.Experience.Columns);
        Assert.Equal("editorial", manifest.Experience.CardStyle);
        Assert.False(manifest.Experience.ShowPrice);
        Assert.True(manifest.Experience.OpenNewTab);
    }

    [Fact]
    public async Task ResolveManifest_ThrowsWhenCollectionMissing()
    {
        await using var db = CreateDb();

        var pub = new Publication
        {
            Key = "orphan001",
            AgencyId = Guid.NewGuid(),
            CollectionId = Guid.NewGuid(),
        };
        // Do NOT add a collection — simulate orphaned publication.

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PublicationResolver.ResolveManifestAsync(pub, db));
    }

    [Fact]
    public async Task ResolveManifest_OffersUrlUsesCollectionId()
    {
        await using var db = CreateDb();

        var collection = new Collection
        {
            AgencyId = Guid.NewGuid(),
            Name = "Egypt Last Minute",
            Slug = "last-minute-egypt",
            Status = CollectionStatus.Published,
        };
        db.Collections.Add(collection);

        var pub = new Publication
        {
            Key = "egypt001",
            AgencyId = collection.AgencyId,
            CollectionId = collection.Id,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();

        var manifest = await PublicationResolver.ResolveManifestAsync(pub, db);

        // Slug is unique only per agency (#18); the public offers URL keys on the
        // global collection id so it is unambiguous across tenants.
        Assert.Equal($"/api/v1/collections/{collection.Id}/offers", manifest.OffersUrl);
    }
}
