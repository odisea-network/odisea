using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Themes.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests;

public class ThemePublishFlowTests
{
    private static AppDbContext BuildDb(string name)
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(opts);
    }

    // PlatformAdmin context: HasAgency = false → ownership checks are skipped.
    private static ThemesController BuildCtrl(AppDbContext db, Guid? agencyId = null) =>
        new(db, new FakeAgencyContext(agencyId));

    [Fact]
    public async Task Publish_sets_status_to_Published_and_increments_version()
    {
        await using var db = BuildDb(nameof(Publish_sets_status_to_Published_and_increments_version));
        var theme = new Theme { Name = "Draft", Status = ThemeStatus.Draft, Version = 1, Tokens = ThemeTokens.Default() };
        db.Themes.Add(theme);
        await db.SaveChangesAsync();

        var result = await BuildCtrl(db).Publish(theme.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<ThemeDto>(ok.Value);
        Assert.Equal("Published", dto.Status);
        Assert.Equal(2, dto.Version);
    }

    [Fact]
    public async Task Publish_already_published_returns_409()
    {
        await using var db = BuildDb(nameof(Publish_already_published_returns_409));
        var theme = new Theme { Name = "Live", Status = ThemeStatus.Published, Version = 2, Tokens = ThemeTokens.Default() };
        db.Themes.Add(theme);
        await db.SaveChangesAsync();

        var result = await BuildCtrl(db).Publish(theme.Id, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, obj.StatusCode);
    }

    [Fact]
    public async Task Publish_unknown_id_returns_404()
    {
        await using var db = BuildDb(nameof(Publish_unknown_id_returns_404));
        var result = await BuildCtrl(db).Publish(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_published_theme_returns_409()
    {
        await using var db = BuildDb(nameof(Update_published_theme_returns_409));
        var theme = new Theme { Name = "Live", Status = ThemeStatus.Published, Version = 1, Tokens = ThemeTokens.Default() };
        db.Themes.Add(theme);
        await db.SaveChangesAsync();

        var result = await BuildCtrl(db).Update(theme.Id, new UpdateThemeRequest("New Name", null), CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, obj.StatusCode);
    }

    [Fact]
    public async Task Create_then_get_round_trips_name_and_tokens()
    {
        await using var db = BuildDb(nameof(Create_then_get_round_trips_name_and_tokens));
        var agencyId = Guid.NewGuid();
        var ctrl = BuildCtrl(db, agencyId);

        var tokens = ThemeTokens.Default();
        tokens.Foundation["accent"] = "#ff0000";

        var createResult = await ctrl.Create(
            new CreateThemeRequest(Guid.NewGuid(), "My Theme", tokens),
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(createResult);
        var dto = Assert.IsType<ThemeDto>(created.Value);

        var getResult = await ctrl.Get(dto.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(getResult);
        var fetched = Assert.IsType<ThemeDto>(ok.Value);

        Assert.Equal("My Theme", fetched.Name);
        Assert.Equal("#ff0000", fetched.Tokens.Foundation["accent"]);
        Assert.Equal("Draft", fetched.Status);
    }
}
