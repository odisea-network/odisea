using Odisea.Modules.Catalog.Domain;
using Xunit;

namespace Odisea.UnitTests.Catalog;

public class ProgramTests
{
    private static ReadinessReport Readiness(Program program, List<ProgramImageInfo>? images = null) =>
        ProgramReadiness.Evaluate(program, images ?? CatalogFixtures.Gallery(), mappedHotels: program.Hotels.Count, CatalogFixtures.Today);

    [Fact]
    public void Publish_requires_the_readiness_report_to_pass()
    {
        var program = CatalogFixtures.Draft();
        var ex = Assert.Throws<InvalidProgramStateException>(() => program.Publish(Readiness(program, [])));
        Assert.Contains("upcoming departure", ex.Message);
        Assert.Contains("allowed hotel", ex.Message);
        Assert.Contains("cover image", ex.Message);
        Assert.Contains("cancellation policy", ex.Message);
        Assert.Equal(ProgramStatus.Draft, program.Status);
    }

    [Fact]
    public void Publish_succeeds_once_the_required_checks_pass()
    {
        var program = CatalogFixtures.Ready();
        program.Publish(Readiness(program));
        Assert.Equal(ProgramStatus.Published, program.Status);
    }

    [Fact]
    public void Recommended_checks_do_not_block_publishing()
    {
        var program = CatalogFixtures.Ready();
        program.Content.Highlights = [];
        program.Content.InsurancePolicy = null;
        program.Itinerary.Clear();

        var readiness = Readiness(program, CatalogFixtures.Gallery(count: 1, coverWidth: 800, altText: false));
        Assert.True(readiness.CanPublish);
        Assert.True(readiness.Score < 100);
        program.Publish(readiness);
        Assert.Equal(ProgramStatus.Published, program.Status);
    }

    [Fact]
    public void Archived_program_cannot_be_republished()
    {
        var program = CatalogFixtures.Ready();
        program.Archive();
        Assert.Throws<InvalidProgramStateException>(() => program.Publish(Readiness(program)));
    }
}
