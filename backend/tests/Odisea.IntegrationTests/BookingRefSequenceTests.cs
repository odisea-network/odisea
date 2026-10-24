using System.Text.RegularExpressions;

namespace Odisea.IntegrationTests;

/// The booking reference comes from a Postgres sequence, which EF InMemory cannot exercise.
[Collection(ApiCollection.Name)]
public class BookingRefSequenceTests(OdiseaApiFactory factory)
{
    private static readonly Regex RefPattern = new(@"^ODI-(\d{4})-(\d{6})$");

    [Fact]
    public async Task Consecutive_drafts_get_consecutive_refs_for_the_check_in_year()
    {
        var agency = factory.ClientAs(TestIdentity.Agent);

        var first = await AgencyFlow.CreateDraftAsync(agency);
        var second = await AgencyFlow.CreateDraftAsync(agency);

        var firstMatch = RefPattern.Match(first.Ref);
        var secondMatch = RefPattern.Match(second.Ref);
        Assert.True(firstMatch.Success, $"Unexpected ref format: {first.Ref}");
        Assert.True(secondMatch.Success, $"Unexpected ref format: {second.Ref}");

        Assert.Equal(first.CheckIn.Year.ToString(), firstMatch.Groups[1].Value);
        Assert.Equal(
            int.Parse(firstMatch.Groups[2].Value) + 1,
            int.Parse(secondMatch.Groups[2].Value));
    }
}
