using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Dtos;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Catalog;

public class CollectionsControllerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CollectionsController CreateController(AppDbContext db, Guid agencyId) =>
        new(db, new FakeAgencyContext(agencyId), new FakeOfferAccessPolicy(db));

    private static CreateCollectionRequest Request(Guid agencyId, string slug) =>
        new(agencyId, $"Collection {slug}", slug, new FilterSpec(), null, null, null);

    [Fact]
    public async Task Create_TwoAgenciesSameSlug_BothSucceed()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var green = Guid.NewGuid();

        var blueResult = await CreateController(db, blue).Create(Request(blue, "summer-greece"), default);
        var greenResult = await CreateController(db, green).Create(Request(green, "summer-greece"), default);

        var blueDto = Assert.IsType<CollectionDto>(Assert.IsType<CreatedAtActionResult>(blueResult).Value);
        var greenDto = Assert.IsType<CollectionDto>(Assert.IsType<CreatedAtActionResult>(greenResult).Value);

        Assert.Equal("summer-greece", blueDto.Slug);
        Assert.Equal("summer-greece", greenDto.Slug);
        Assert.Equal(blue, blueDto.AgencyId);
        Assert.Equal(green, greenDto.AgencyId);
    }

    [Fact]
    public async Task Create_SameAgencyDuplicateSlug_Returns409()
    {
        await using var db = CreateDb();
        var agency = Guid.NewGuid();
        var controller = CreateController(db, agency);

        Assert.IsType<CreatedAtActionResult>(
            await controller.Create(Request(agency, "summer-greece"), default));

        var conflict = Assert.IsType<ObjectResult>(
            await controller.Create(Request(agency, "summer-greece"), default));
        Assert.Equal(409, conflict.StatusCode);
    }

    [Fact]
    public async Task Get_BySlug_ReturnsCallingAgencysCollection()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var green = Guid.NewGuid();

        // Both agencies own the same slug; lookup must be scoped to the caller.
        Assert.IsType<CreatedAtActionResult>(
            await CreateController(db, blue).Create(Request(blue, "summer-greece"), default));
        Assert.IsType<CreatedAtActionResult>(
            await CreateController(db, green).Create(Request(green, "summer-greece"), default));

        var blueOk = Assert.IsType<OkObjectResult>(
            await CreateController(db, blue).Get("summer-greece", default));
        var blueDto = Assert.IsType<CollectionDto>(blueOk.Value);
        Assert.Equal(blue, blueDto.AgencyId);

        var greenOk = Assert.IsType<OkObjectResult>(
            await CreateController(db, green).Get("summer-greece", default));
        var greenDto = Assert.IsType<CollectionDto>(greenOk.Value);
        Assert.Equal(green, greenDto.AgencyId);
    }

    [Fact]
    public async Task Get_BySlug_DoesNotLeakAnotherAgencysCollection()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var green = Guid.NewGuid();

        Assert.IsType<CreatedAtActionResult>(
            await CreateController(db, blue).Create(Request(blue, "blue-only"), default));

        // Green has no "blue-only" slug — must not see Blue's collection.
        Assert.IsType<NotFoundResult>(
            await CreateController(db, green).Get("blue-only", default));
    }

    [Fact]
    public async Task List_ReturnsOnlyCallingAgencysCollections()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var green = Guid.NewGuid();
        await CreateController(db, blue).Create(Request(blue, "blue-1"), default);
        await CreateController(db, green).Create(Request(green, "green-1"), default);

        var ok = Assert.IsType<OkObjectResult>(await CreateController(db, blue).List(default));
        var list = Assert.IsAssignableFrom<IEnumerable<CollectionDto>>(ok.Value);
        Assert.Single(list);
        Assert.All(list, c => Assert.Equal(blue, c.AgencyId));
    }

    [Fact]
    public async Task Get_ById_AnotherAgencysCollection_Returns403()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var green = Guid.NewGuid();

        var created = Assert.IsType<CreatedAtActionResult>(
            await CreateController(db, blue).Create(Request(blue, "blue-only"), default));
        var blueId = Assert.IsType<CollectionDto>(created.Value).Id;

        // Green knows the global id but must be refused.
        var result = await CreateController(db, green).Get(blueId.ToString(), default);
        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, problem.StatusCode);
    }

    // ── Resolve (embed offers endpoint) — publishable-key agency gate ────────────

    private static async Task<Guid> SeedCollection(AppDbContext db, Guid agencyId, string slug)
    {
        var created = Assert.IsType<CreatedAtActionResult>(
            await CreateController(db, agencyId).Create(Request(agencyId, slug), default));
        return Assert.IsType<CollectionDto>(created.Value).Id;
    }

    [Fact]
    public async Task Resolve_KeyBelongsToCollectionsAgency_Returns200()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var id = await SeedCollection(db, blue, "blue-offers");

        // The embed key resolves to Blue's agency; it owns the collection.
        var result = await CreateController(db, blue).Resolve(id.ToString(), default);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Resolve_KeyBelongsToAnotherAgency_Returns403()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var green = Guid.NewGuid();
        var id = await SeedCollection(db, blue, "blue-offers");

        // A valid key for Green cannot resolve Blue's collection by its global id.
        var result = await CreateController(db, green).Resolve(id.ToString(), default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, problem.StatusCode);
    }

    [Fact]
    public async Task Resolve_PlatformAdminNoAgency_Returns200()
    {
        await using var db = CreateDb();
        var blue = Guid.NewGuid();
        var id = await SeedCollection(db, blue, "blue-offers");

        // PlatformAdmin token carries no agency claim; the ownership gate is skipped.
        var controller = new CollectionsController(db, new FakeAgencyContext(null), new FakeOfferAccessPolicy(db));
        var result = await controller.Resolve(id.ToString(), default);

        Assert.IsType<OkObjectResult>(result);
    }
}
