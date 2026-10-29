using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Programs;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.SharedKernel;
using Xunit;
using CatalogProgram = Odisea.Modules.Catalog.Domain.Program;

namespace Odisea.UnitTests.Catalog;

/// Content rules, itinerary replacement, slugs, the gallery and publishing over an InMemory context.
public class ProgramServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(CatalogFixtures.Today, TimeOnly.MinValue, TimeSpan.Zero);
    }

    private readonly CatalogDbContext _db;
    private readonly ProgramImageService _images;
    private readonly ProgramService _service;

    public ProgramServiceTests()
    {
        _db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _images = new ProgramImageService(_db);
        _service = new ProgramService(_db, _images, new FakeClock());
    }

    private async Task<CatalogProgram> SeedAsync(string name = "Antalya Easter 2027")
    {
        var program = CatalogFixtures.Draft(name);
        _db.Programs.Add(program);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return program;
    }

    private Task<ProgramDetailDto> SaveAsync(Guid id, SaveProgramContentRequest request) =>
        _service.SaveContentAsync(id, request, CancellationToken.None);

    [Fact]
    public async Task Content_is_trimmed_and_the_itinerary_replaced_as_a_whole()
    {
        var program = await SeedAsync();
        var request = CatalogFixtures.FullContentRequest() with
        {
            Highlights = ["  Direct charter ", "", "   ", "Three hotels"],
            Tags = [" Family", "family", "all-inclusive"],
            Itinerary = CatalogFixtures.Days(3),
        };

        var detail = await SaveAsync(program.Id, request);
        Assert.Equal(["Direct charter", "Three hotels"], detail.Content.Highlights);
        Assert.Equal(["family", "all-inclusive"], detail.Content.Tags);
        Assert.Equal([1, 2, 3], detail.Itinerary.Select(d => d.Day));

        _db.ChangeTracker.Clear();
        detail = await SaveAsync(program.Id, request with { Itinerary = CatalogFixtures.Days(2) });
        Assert.Equal(2, detail.Itinerary.Count);
        Assert.Equal(2, await _db.ProgramItineraryDays.CountAsync(d => d.ProgramId == program.Id));
    }

    [Fact]
    public async Task Rejects_gapped_itineraries_bad_tags_and_inverted_group_sizes()
    {
        var program = await SeedAsync();
        var ok = CatalogFixtures.FullContentRequest();

        await Assert.ThrowsAsync<ProgramValidationException>(() => SaveAsync(program.Id,
            ok with { Itinerary = [new ItineraryDayRequest(1, "A", null, MealPlan.None, null), new ItineraryDayRequest(3, "C", null, MealPlan.None, null)] }));
        await Assert.ThrowsAsync<ProgramValidationException>(() => SaveAsync(program.Id, ok with { Tags = ["All Inclusive"] }));
        await Assert.ThrowsAsync<ProgramValidationException>(() => SaveAsync(program.Id, ok with { MinGroupSize = 20, MaxGroupSize = 10 }));
        await Assert.ThrowsAsync<ProgramValidationException>(() => SaveAsync(program.Id,
            ok with { Highlights = [.. Enumerable.Repeat("x", ProgramContent.MaxHighlights + 1)] }));
    }

    [Fact]
    public async Task Slugs_are_validated_and_unique()
    {
        var first = await SeedAsync("Antalya Easter 2027");
        var second = await SeedAsync("Bodrum Summer 2027");

        await Assert.ThrowsAsync<ProgramValidationException>(() =>
            SaveAsync(second.Id, CatalogFixtures.FullContentRequest(slug: "Not A Slug")));
        var clash = await Assert.ThrowsAsync<ProgramValidationException>(() =>
            SaveAsync(second.Id, CatalogFixtures.FullContentRequest(slug: first.Slug)));
        Assert.Contains("already used", clash.Message);

        var detail = await SaveAsync(second.Id, CatalogFixtures.FullContentRequest(slug: "bodrum-2027"));
        Assert.Equal("bodrum-2027", detail.Slug);
    }

    [Fact]
    public async Task Unique_slug_appends_a_counter_when_the_name_is_taken()
    {
        await SeedAsync("Antalya Easter 2027");
        Assert.Equal("antalya-easter-2027-2", await _service.UniqueSlugAsync("Antalya Easter 2027", CancellationToken.None));
        Assert.Equal("bodrum", await _service.UniqueSlugAsync("Bodrum", CancellationToken.None));
        Assert.Equal("program", await _service.UniqueSlugAsync("***", CancellationToken.None));
    }

    [Fact]
    public async Task Publishing_waits_for_the_required_content_and_a_cover()
    {
        var program = await SeedAsync();
        _db.ProgramDepartures.Add(new ProgramDeparture
        {
            ProgramId = program.Id, StartDate = new DateOnly(2027, 3, 30), EndDate = new DateOnly(2027, 4, 6),
        });
        _db.ProgramHotels.Add(new ProgramHotel { ProgramId = program.Id, HotelId = Guid.NewGuid() });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var blocked = await Assert.ThrowsAsync<InvalidProgramStateException>(() => _service.PublishAsync(program.Id, CancellationToken.None));
        Assert.Contains("summary", blocked.Message);
        Assert.Contains("cover image", blocked.Message);

        await SaveAsync(program.Id, CatalogFixtures.FullContentRequest());
        _db.ChangeTracker.Clear();
        var stillBlocked = await Assert.ThrowsAsync<InvalidProgramStateException>(() => _service.PublishAsync(program.Id, CancellationToken.None));
        Assert.DoesNotContain("summary", stillBlocked.Message);
        Assert.Contains("cover image", stillBlocked.Message);

        await _images.UploadAsync(program.Id, ImageFileTests.PngHeader(1600, 900), "Beach", "Sandy beach", isCover: false, CancellationToken.None);
        _db.ChangeTracker.Clear();
        var detail = await _service.PublishAsync(program.Id, CancellationToken.None);
        Assert.Equal("Published", detail.Status);
        Assert.NotNull(detail.Readiness);
        Assert.True(detail.Readiness.CanPublish);
    }

    [Fact]
    public async Task Gallery_keeps_exactly_one_cover_and_an_explicit_order()
    {
        var program = await SeedAsync();
        var ct = CancellationToken.None;

        var first = await _images.UploadAsync(program.Id, ImageFileTests.PngHeader(1600, 900), null, "One", false, ct);
        var second = await _images.UploadAsync(program.Id, ImageFileTests.PngHeader(800, 600), null, "Two", true, ct);
        var third = await _images.UploadAsync(program.Id, ImageFileTests.PngHeader(800, 600), null, null, false, ct);
        Assert.True(first.IsCover);
        Assert.True(second.IsCover);

        var list = await _images.ListAsync(program.Id, ct);
        Assert.Equal([first.Id, second.Id, third.Id], list.Select(i => i.Id));
        Assert.Equal(second.Id, list.Single(i => i.IsCover).Id);

        await Assert.ThrowsAsync<ProgramValidationException>(() => _images.ReorderAsync(program.Id, [third.Id, first.Id], ct));
        list = await _images.ReorderAsync(program.Id, [third.Id, first.Id, second.Id], ct);
        Assert.Equal([third.Id, first.Id, second.Id], list.Select(i => i.Id));

        await Assert.ThrowsAsync<ProgramValidationException>(() =>
            _images.UpdateAsync(program.Id, second.Id, new UpdateProgramImageRequest("Pool", "Pool", IsCover: false), ct));
        await _images.DeleteAsync(program.Id, second.Id, ct);
        list = await _images.ListAsync(program.Id, ct);
        Assert.Equal(third.Id, list.Single(i => i.IsCover).Id);

        await Assert.ThrowsAsync<ProgramImageNotFoundException>(() => _images.DeleteAsync(Guid.NewGuid(), third.Id, ct));
        Assert.Equal(ProgramDtoMapping.ImageUrl(third.Id), list[0].Url);
    }
}
