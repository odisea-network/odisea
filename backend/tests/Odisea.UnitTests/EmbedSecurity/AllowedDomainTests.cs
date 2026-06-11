using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Publications.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.EmbedSecurity;

public class AllowedDomainTests
{
    [Theory]
    [InlineData("agency.bg", "agency.bg", true)]
    [InlineData("agency.bg", "AGENCY.BG", true)]      // case-insensitive
    [InlineData("agency.bg", "other.bg", false)]
    [InlineData("*.example.com", "shop.example.com", true)]
    [InlineData("*.example.com", "a.b.example.com", true)]
    [InlineData("*.example.com", "example.com", true)] // wildcard also matches apex
    [InlineData("*.example.com", "example.org", false)]
    [InlineData("*.example.com", "notexample.com", false)]
    public void Matches_HandlesExactAndWildcard(string domain, string host, bool expected)
    {
        var entry = new AllowedDomain { Domain = domain };
        Assert.Equal(expected, entry.Matches(host));
    }

    [Fact]
    public void Matches_EmptyHost_False()
    {
        Assert.False(new AllowedDomain { Domain = "agency.bg" }.Matches(""));
    }
}

public class AllowedDomainsControllerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(AppDbContext db, Publication pub)> SeedPublication(Guid agencyId)
    {
        var db = CreateDb();
        var pub = new Publication
        {
            Key = "pubkey0001",
            AgencyId = agencyId,
            CollectionId = Guid.NewGuid(),
            Status = PublicationStatus.Draft,
        };
        db.Publications.Add(pub);
        await db.SaveChangesAsync();
        return (db, pub);
    }

    [Fact]
    public async Task Replace_NormalizesAndPersistsDomains()
    {
        var agencyId = Guid.NewGuid();
        var (db, pub) = await SeedPublication(agencyId);
        await using var _ = db;

        var controller = new AllowedDomainsController(db, new FakeAgencyContext(agencyId));
        var result = await controller.Replace(
            pub.Id, new ManageDomainsRequest(["Agency-Blue.BG", "agency-blue.bg", "*.example.com"]), default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<AllowedDomainDto>>(ok.Value);

        // Dedup + lowercase: "Agency-Blue.BG" and "agency-blue.bg" collapse to one.
        Assert.Equal(2, dtos.Count());
        Assert.Contains(dtos, d => d.Domain == "agency-blue.bg");
        Assert.Contains(dtos, d => d.Domain == "*.example.com");
    }

    [Fact]
    public async Task Replace_InvalidDomain_Returns400()
    {
        var agencyId = Guid.NewGuid();
        var (db, pub) = await SeedPublication(agencyId);
        await using var _ = db;

        var controller = new AllowedDomainsController(db, new FakeAgencyContext(agencyId));
        var result = await controller.Replace(
            pub.Id, new ManageDomainsRequest(["https://has-scheme.com"]), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, obj.StatusCode);
    }

    [Fact]
    public async Task Replace_EmptyList_OpensPublication()
    {
        var agencyId = Guid.NewGuid();
        var (db, pub) = await SeedPublication(agencyId);
        db.AllowedDomains.Add(new AllowedDomain { PublicationId = pub.Id, Domain = "agency.bg" });
        await db.SaveChangesAsync();
        await using var _ = db;

        var controller = new AllowedDomainsController(db, new FakeAgencyContext(agencyId));
        var result = await controller.Replace(pub.Id, new ManageDomainsRequest([]), default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<AllowedDomainDto>>(ok.Value);
        Assert.Empty(dtos);
    }

    [Fact]
    public async Task Replace_ForeignAgency_Returns403()
    {
        var (db, pub) = await SeedPublication(Guid.NewGuid());
        await using var _ = db;

        var controller = new AllowedDomainsController(db, new FakeAgencyContext(Guid.NewGuid()));
        var result = await controller.Replace(pub.Id, new ManageDomainsRequest(["agency.bg"]), default);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, obj.StatusCode);
    }
}
