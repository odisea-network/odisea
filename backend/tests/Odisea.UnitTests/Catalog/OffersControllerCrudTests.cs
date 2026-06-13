using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Catalog;

public class OffersControllerCrudTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static OffersController ControllerFor(AppDbContext db, Guid operatorId) =>
        new(db, new FakeOperatorContext(operatorId));

    private static CreateOfferRequest ValidCreate() => new(
        Title: "Antalya Family AI",
        Description: "7-night all-inclusive",
        Country: "TR",
        City: "Antalya",
        Price: 690m,
        Currency: "EUR",
        BoardBasis: "AllInclusive",
        Transport: "Plane",
        DurationNights: 7,
        StartDate: null,
        EndDate: null,
        Tags: ["family"],
        ImageUrl: null);

    [Fact]
    public async Task Create_SetsOperatorOwnershipAndDraftStatus()
    {
        await using var db = NewDb();
        var operatorId = Guid.NewGuid();
        var controller = ControllerFor(db, operatorId);

        var result = await controller.Create(ValidCreate(), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<OfferDto>(created.Value);
        Assert.Equal("Antalya Family AI", dto.Title);

        var saved = await db.Offers.FirstAsync();
        Assert.Equal(operatorId, saved.OwningOperatorId);
        Assert.Equal(OwnerType.Operator, saved.OwnerType);
        Assert.Equal(Visibility.PlatformShared, saved.Visibility);
        Assert.Equal(OfferStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task Create_InvalidBoardBasis_Returns400()
    {
        await using var db = NewDb();
        var controller = ControllerFor(db, Guid.NewGuid());

        var result = await controller.Create(ValidCreate() with { BoardBasis = "Caviar" }, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }

    [Fact]
    public async Task Mine_ReturnsOnlyCallingOperatorsOffers()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        db.Offers.AddRange(
            new Offer { Title = "Mine", Country = "GR", City = "A", Price = 1, BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7, OwnerType = OwnerType.Operator, OwningOperatorId = mine },
            new Offer { Title = "Theirs", Country = "GR", City = "B", Price = 1, BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7, OwnerType = OwnerType.Operator, OwningOperatorId = theirs });
        await db.SaveChangesAsync();

        var result = await ControllerFor(db, mine).Mine(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var offers = Assert.IsAssignableFrom<IEnumerable<OfferDto>>(ok.Value);
        Assert.Single(offers);
        Assert.Equal("Mine", offers.First().Title);
    }

    [Fact]
    public async Task Update_AnotherOperatorsOffer_Returns404()
    {
        await using var db = NewDb();
        var owner = Guid.NewGuid();
        var offer = new Offer { Title = "Owned", Country = "GR", City = "A", Price = 1, BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7, OwnerType = OwnerType.Operator, OwningOperatorId = owner };
        db.Offers.Add(offer);
        await db.SaveChangesAsync();

        // A different operator tries to edit it.
        var controller = ControllerFor(db, Guid.NewGuid());
        var req = new UpdateOfferRequest("X", "", "GR", "A", 2, "EUR", "HalfBoard", "Plane", 7, null, null, null, null);

        var result = await controller.Update(offer.Id, req, default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task PublishThenUnpublish_FlipsStatus()
    {
        await using var db = NewDb();
        var operatorId = Guid.NewGuid();
        var offer = new Offer { Title = "O", Country = "GR", City = "A", Price = 1, BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7, OwnerType = OwnerType.Operator, OwningOperatorId = operatorId, Status = OfferStatus.Draft };
        db.Offers.Add(offer);
        await db.SaveChangesAsync();
        var controller = ControllerFor(db, operatorId);

        await controller.Publish(offer.Id, default);
        Assert.Equal(OfferStatus.Published, (await db.Offers.FirstAsync()).Status);

        await controller.Unpublish(offer.Id, default);
        Assert.Equal(OfferStatus.Draft, (await db.Offers.FirstAsync()).Status);
    }

    [Fact]
    public async Task BulkCreate_CreatesValidRows_AndReportsInvalidByIndex()
    {
        await using var db = NewDb();
        var operatorId = Guid.NewGuid();
        var controller = ControllerFor(db, operatorId);

        var good = ValidCreate();
        var badBoard = good with { Title = "Bad", BoardBasis = "Caviar" };
        var badCountry = good with { Title = "NoCountry", Country = "" };

        var result = await controller.BulkCreate(
            new BulkCreateOffersRequest([good, badBoard, good, badCountry]), default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<BulkCreateResultDto>(ok.Value);
        Assert.Equal(2, dto.Created);
        Assert.Equal(2, dto.Errors.Count);
        Assert.Equal(1, dto.Errors[0].Index);   // badBoard at index 1
        Assert.Equal(3, dto.Errors[1].Index);   // badCountry at index 3

        var saved = await db.Offers.ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, o => Assert.Equal(operatorId, o.OwningOperatorId));
        Assert.All(saved, o => Assert.Equal(OfferStatus.Draft, o.Status));
    }

    [Fact]
    public async Task BulkCreate_EmptyList_Returns400()
    {
        await using var db = NewDb();
        var result = await ControllerFor(db, Guid.NewGuid())
            .BulkCreate(new BulkCreateOffersRequest([]), default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }
}
