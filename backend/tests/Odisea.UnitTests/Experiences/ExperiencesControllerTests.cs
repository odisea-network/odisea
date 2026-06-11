using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Experiences.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Experiences;

public class ExperiencesControllerTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ExperiencesController CreateController(AppDbContext db, Guid? agencyId = null) =>
        new(db, new FakeAgencyContext(agencyId));

    [Fact]
    public async Task Create_ValidRequest_Returns201WithDraft()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        var controller = CreateController(db, agencyId);

        var req = new CreateExperienceRequest(agencyId, "Homepage grid", null);
        var result = await controller.Create(req, default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<ExperienceDto>(created.Value);
        Assert.Equal("Homepage grid", dto.Name);
        Assert.Equal("Draft", dto.Status);
        Assert.Equal(1, dto.Version);
        Assert.Equal(agencyId, dto.AgencyId);
        // Null config falls back to the record defaults (grid / 3 columns).
        Assert.Equal("grid", dto.Config.Type);
        Assert.Equal(3, dto.Config.Columns);
    }

    [Fact]
    public async Task Create_PersistsProvidedConfig()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        var controller = CreateController(db, agencyId);

        var config = new ExperienceConfig(Type: "carousel", Columns: 2, CardStyle: "compact",
            ShowPrice: false, Inquiry: true, OpenNewTab: true);
        var req = new CreateExperienceRequest(agencyId, "Carousel", config);

        var created = Assert.IsType<CreatedAtActionResult>(await controller.Create(req, default));
        var dto = Assert.IsType<ExperienceDto>(created.Value);

        Assert.Equal("carousel", dto.Config.Type);
        Assert.Equal(2, dto.Config.Columns);
        Assert.False(dto.Config.ShowPrice);
        Assert.True(dto.Config.OpenNewTab);
    }

    [Fact]
    public async Task Get_ReturnsCreatedExperience()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        var controller = CreateController(db, agencyId);

        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(new CreateExperienceRequest(agencyId, "Showcase", null), default));
        var createdDto = Assert.IsType<ExperienceDto>(created.Value);

        var ok = Assert.IsType<OkObjectResult>(await controller.Get(createdDto.Id, default));
        var fetched = Assert.IsType<ExperienceDto>(ok.Value);
        Assert.Equal("Showcase", fetched.Name);
    }

    [Fact]
    public async Task Get_UnknownId_Returns404()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        Assert.IsType<NotFoundResult>(await controller.Get(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task List_ScopesToAgency()
    {
        await using var db = CreateDb();
        var agencyId = Guid.NewGuid();
        db.Experiences.Add(new Experience { AgencyId = agencyId, Name = "Mine" });
        db.Experiences.Add(new Experience { AgencyId = Guid.NewGuid(), Name = "Theirs" });
        await db.SaveChangesAsync();

        var controller = CreateController(db, agencyId);
        var ok = Assert.IsType<OkObjectResult>(await controller.List(default));
        var list = Assert.IsAssignableFrom<IEnumerable<ExperienceDto>>(ok.Value);

        var only = Assert.Single(list);
        Assert.Equal("Mine", only.Name);
    }

    [Fact]
    public async Task Publish_FlipsStatusAndIncrementsVersion()
    {
        await using var db = CreateDb();
        var experience = new Experience { AgencyId = Guid.NewGuid(), Name = "Draft", Status = ExperienceStatus.Draft, Version = 1 };
        db.Experiences.Add(experience);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var ok = Assert.IsType<OkObjectResult>(await controller.Publish(experience.Id, default));
        var dto = Assert.IsType<ExperienceDto>(ok.Value);

        Assert.Equal("Published", dto.Status);
        Assert.Equal(2, dto.Version);
    }

    [Fact]
    public async Task Publish_AlreadyPublished_Returns409()
    {
        await using var db = CreateDb();
        var experience = new Experience { AgencyId = Guid.NewGuid(), Name = "Live", Status = ExperienceStatus.Published, Version = 2 };
        db.Experiences.Add(experience);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var obj = Assert.IsType<ObjectResult>(await controller.Publish(experience.Id, default));
        Assert.Equal(409, obj.StatusCode);
    }

    [Fact]
    public async Task Update_PublishedExperience_Returns409()
    {
        await using var db = CreateDb();
        var experience = new Experience { AgencyId = Guid.NewGuid(), Name = "Live", Status = ExperienceStatus.Published, Version = 1 };
        db.Experiences.Add(experience);
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var obj = Assert.IsType<ObjectResult>(
            await controller.Update(experience.Id, new UpdateExperienceRequest("Renamed", null), default));
        Assert.Equal(409, obj.StatusCode);
    }
}
