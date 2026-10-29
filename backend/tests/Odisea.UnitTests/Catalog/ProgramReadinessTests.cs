using Odisea.Modules.Catalog.Domain;
using Xunit;

namespace Odisea.UnitTests.Catalog;

public class ProgramReadinessTests
{
    private static ReadinessCheck Check(ReadinessReport report, string code) => report.Checks.Single(c => c.Code == code);

    [Fact]
    public void Bare_program_fails_every_required_check()
    {
        var report = ProgramReadiness.Evaluate(CatalogFixtures.Draft(), [], mappedHotels: 0, CatalogFixtures.Today);

        Assert.False(report.CanPublish);
        Assert.Equal(["departures", "hotels", "summary", "cover", "inclusions", "cancellation"],
            report.Blocking.Select(c => c.Code).ToArray());
        Assert.True(report.Score < 20);
    }

    [Fact]
    public void Complete_program_scores_100()
    {
        var program = CatalogFixtures.Ready();
        var report = ProgramReadiness.Evaluate(program, CatalogFixtures.Gallery(), mappedHotels: 1, CatalogFixtures.Today);

        Assert.True(report.CanPublish);
        Assert.Equal(100, report.Score);
        Assert.All(report.Checks, c => Assert.True(c.Passed, $"{c.Code}: {c.Detail}"));
    }

    [Fact]
    public void Itinerary_must_describe_every_day_of_the_trip()
    {
        var program = CatalogFixtures.Ready();
        program.Itinerary.RemoveAt(7);

        var check = Check(ProgramReadiness.Evaluate(program, CatalogFixtures.Gallery(), 1, CatalogFixtures.Today), "itinerary");
        Assert.False(check.Passed);
        Assert.Equal("7 days described for a 8-day program", check.Detail);
    }

    [Fact]
    public void Departures_that_already_started_do_not_count()
    {
        var program = CatalogFixtures.Ready();
        var report = ProgramReadiness.Evaluate(program, CatalogFixtures.Gallery(), 1, today: new DateOnly(2027, 4, 1));

        Assert.False(report.CanPublish);
        Assert.Equal("None yet", Check(report, "departures").Detail);
    }

    [Fact]
    public void Gallery_checks_look_at_cover_size_alt_text_and_count()
    {
        var program = CatalogFixtures.Ready();
        var report = ProgramReadiness.Evaluate(program, CatalogFixtures.Gallery(count: 2, coverWidth: 1000, altText: false), 1, CatalogFixtures.Today);

        Assert.True(report.CanPublish);
        Assert.False(Check(report, "gallery").Passed);
        Assert.False(Check(report, "cover-size").Passed);
        Assert.Equal("2 without", Check(report, "alt-text").Detail);
    }

    [Fact]
    public void Unmapped_hotels_and_a_long_title_are_flagged()
    {
        var program = CatalogFixtures.Draft(new string('A', 80));
        var report = ProgramReadiness.Evaluate(program, [], 0, CatalogFixtures.Today);

        Assert.False(Check(report, "title").Passed);
        Assert.False(Check(report, "mappings").Passed);
        Assert.Equal("Mode not set", Check(report, "transport").Detail);
    }
}
