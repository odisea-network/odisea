using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.SharedKernel;

public class DateRangeTests
{
    private static DateOnly D(string value) => DateOnly.Parse(value);

    [Fact]
    public void Rejects_end_before_start()
    {
        Assert.Throws<ArgumentException>(() => new DateRange(D("2027-04-06"), D("2027-03-30")));
    }

    [Fact]
    public void Computes_nights()
    {
        var stay = new DateRange(D("2027-03-30"), D("2027-04-06"));
        Assert.Equal(7, stay.Nights);
    }

    [Theory]
    [InlineData("2027-03-30", "2027-04-02", true)]   // starts inside
    [InlineData("2027-04-06", "2027-04-10", true)]   // touches the end
    [InlineData("2027-04-07", "2027-04-10", false)]  // after
    public void Detects_overlaps(string from, string to, bool expected)
    {
        var stay = new DateRange(D("2027-03-30"), D("2027-04-06"));
        Assert.Equal(expected, stay.Overlaps(new DateRange(D(from), D(to))));
    }
}
