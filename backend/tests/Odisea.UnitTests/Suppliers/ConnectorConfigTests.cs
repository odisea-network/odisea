using Odisea.Infrastructure.Suppliers.Connectors;

namespace Odisea.UnitTests.Suppliers;

public class ConnectorConfigTests
{
    [Fact]
    public void Field_returnsMappedName_whenPresent()
    {
        var config = ConnectorConfig.Parse("""{"url":"u","fieldMap":{"externalId":"id"}}""")!;
        Assert.Equal("id", config.Field("externalId"));
    }

    [Fact]
    public void Field_fallsBackToCanonical_whenUnmappedOrBlank()
    {
        var config = ConnectorConfig.Parse("""{"url":"u","fieldMap":{"title":"  "}}""")!;
        Assert.Equal("title", config.Field("title"));   // blank override ignored
        Assert.Equal("price", config.Field("price"));   // not in the map
    }

    [Fact]
    public void Field_fallsBackToCanonical_whenNoMapAtAll()
    {
        var config = ConnectorConfig.Parse("""{"url":"u"}""")!;
        Assert.Equal("country", config.Field("country"));
    }

    [Fact]
    public void Parse_invalidJson_returnsNull()
    {
        Assert.Null(ConnectorConfig.Parse("not json"));
    }
}
