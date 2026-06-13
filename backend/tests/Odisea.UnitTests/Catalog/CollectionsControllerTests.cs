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
}
