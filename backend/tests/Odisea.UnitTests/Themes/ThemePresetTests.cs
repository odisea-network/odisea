using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Themes.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Themes;

public class ThemePresetTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static Theme Preset(string name) => new()
    {
        AgencyId = Guid.Empty, Name = name, IsPreset = true, Status = ThemeStatus.Published,
        Tokens = new ThemeTokens { Foundation = new() { ["accent"] = "#abc" } },
    };

    private static ThemesController Controller(AppDbContext db, Guid? agencyId = null) =>
        new(db, new FakeAgencyContext(agencyId));

    [Fact]
    public async Task Presets_ReturnsOnlyPresets()
    {
        await using var db = NewDb();
        db.Themes.AddRange(
            Preset("Coastal"),
            new Theme { AgencyId = Guid.NewGuid(), Name = "Agency theme", IsPreset = false });
        await db.SaveChangesAsync();

        var result = await Controller(db).Presets(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var themes = Assert.IsAssignableFrom<IEnumerable<ThemeDto>>(ok.Value);
        Assert.Single(themes);
        Assert.Equal("Coastal", themes.First().Name);
        Assert.True(themes.First().IsPreset);
    }

    [Fact]
    public async Task List_ExcludesPresets()
    {
        await using var db = NewDb();
        var agencyId = Guid.NewGuid();
        db.Themes.AddRange(
            Preset("Coastal"),
            new Theme { AgencyId = agencyId, Name = "Mine", IsPreset = false });
        await db.SaveChangesAsync();

        var result = await Controller(db, agencyId).List(agencyId, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var themes = Assert.IsAssignableFrom<IEnumerable<ThemeDto>>(ok.Value);
        Assert.Single(themes);
        Assert.Equal("Mine", themes.First().Name);
    }

    [Fact]
    public async Task CloneFromPreset_CreatesAgencyOwnedDraftWithCopiedTokens()
    {
        await using var db = NewDb();
        var preset = Preset("Coastal");
        db.Themes.Add(preset);
        await db.SaveChangesAsync();
        var agencyId = Guid.NewGuid();

        var result = await Controller(db, agencyId).CloneFromPreset(preset.Id, new CloneFromPresetRequest(null), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var dto = Assert.IsType<ThemeDto>(created.Value);
        Assert.Equal(agencyId, dto.AgencyId);
        Assert.False(dto.IsPreset);
        Assert.Equal("Draft", dto.Status);
        Assert.Equal("Coastal (copy)", dto.Name);
        Assert.Equal("#abc", dto.Tokens.Foundation["accent"]);

        // Editing the clone must not mutate the preset (deep copy).
        var clone = await db.Themes.FirstAsync(t => t.Id == dto.Id);
        clone.Tokens.Foundation["accent"] = "#zzz";
        var reloadedPreset = await db.Themes.FirstAsync(t => t.Id == preset.Id);
        Assert.Equal("#abc", reloadedPreset.Tokens.Foundation["accent"]);
    }

    [Fact]
    public async Task CloneFromPreset_CustomName_IsUsed()
    {
        await using var db = NewDb();
        var preset = Preset("Coastal");
        db.Themes.Add(preset);
        await db.SaveChangesAsync();

        var result = await Controller(db, Guid.NewGuid())
            .CloneFromPreset(preset.Id, new CloneFromPresetRequest("Summer Brand"), default);

        var dto = Assert.IsType<ThemeDto>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.Equal("Summer Brand", dto.Name);
    }

    [Fact]
    public async Task CloneFromPreset_NonPresetId_Returns404()
    {
        await using var db = NewDb();
        var notPreset = new Theme { AgencyId = Guid.NewGuid(), Name = "Regular", IsPreset = false };
        db.Themes.Add(notPreset);
        await db.SaveChangesAsync();

        var result = await Controller(db, Guid.NewGuid())
            .CloneFromPreset(notPreset.Id, new CloneFromPresetRequest(null), default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, problem.StatusCode);
    }

    [Fact]
    public async Task CloneFromPreset_PlatformAdminWithoutAgency_Returns400()
    {
        await using var db = NewDb();
        var preset = Preset("Coastal");
        db.Themes.Add(preset);
        await db.SaveChangesAsync();

        // No agency context = PlatformAdmin.
        var result = await Controller(db, agencyId: null)
            .CloneFromPreset(preset.Id, new CloneFromPresetRequest(null), default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, problem.StatusCode);
    }
}
