using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.SharedKernel;

public class PaxTests
{
    [Fact]
    public void Requires_at_least_one_adult()
    {
        Assert.Throws<ArgumentException>(() => new Pax(0));
    }

    [Fact]
    public void Rejects_negative_children()
    {
        Assert.Throws<ArgumentException>(() => new Pax(2, -1));
    }

    [Fact]
    public void Totals_adults_and_children()
    {
        Assert.Equal(4, new Pax(2, 2).Total);
    }
}
