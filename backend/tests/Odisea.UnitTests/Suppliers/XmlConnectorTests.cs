using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Suppliers.Connectors;

namespace Odisea.UnitTests.Suppliers;

public class XmlConnectorTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private sealed class StubHandler(string? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (body is null) throw new HttpRequestException("connection refused");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/xml"),
            });
        }
    }

    private static XmlConnector Connector(string? body, AppDbContext db) =>
        new(new HttpClient(new StubHandler(body)), new SourceOfferImporter(db));

    private static SupplierConnection Connection(Guid? operatorId = null) => new()
    {
        OperatorId = operatorId ?? Guid.NewGuid(),
        Kind = SupplierConnectionKind.Xml,
        Name = "XML feed",
        ConfigJson = """{"url":"https://supplier.test/feed.xml"}""",
    };

    private const string TwoValidOffers = """
    <offers>
      <offer>
        <externalId>crete-1</externalId>
        <title>Crete Sun</title>
        <country>GR</country>
        <city>Heraklion</city>
        <price>540.50</price>
        <currency>EUR</currency>
        <board>HalfBoard</board>
        <transport>Plane</transport>
        <nights>7</nights>
        <imageUrl>https://img/1</imageUrl>
        <tags><tag>beach</tag><tag>family</tag></tags>
      </offer>
      <offer>
        <externalId>antalya-1</externalId>
        <title>Antalya AI</title>
        <country>TR</country>
        <price>690</price>
        <board>AllInclusive</board>
        <transport>Plane</transport>
        <nights>7</nights>
      </offer>
    </offers>
    """;

    [Fact]
    public async Task Imports_validOffers_asPublishedOperatorOwnedSourcedOffers()
    {
        await using var db = NewDb();
        var operatorId = Guid.NewGuid();
        var conn = Connection(operatorId);

        var result = await Connector(TwoValidOffers, db).RunAsync(conn, default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.OffersFetched);
        Assert.Equal(2, result.OffersImported);

        var crete = await db.Offers.SingleAsync(o => o.Source!.ExternalId == "crete-1");
        Assert.Equal("Crete Sun", crete.Title);
        Assert.Equal("Heraklion", crete.City);
        Assert.Equal(540.50m, crete.Price);
        Assert.Equal(BoardBasis.HalfBoard, crete.BoardBasis);
        Assert.Equal(7, crete.DurationNights);
        Assert.Equal(["beach", "family"], crete.Tags);
        Assert.Equal(OfferStatus.Published, crete.Status);
        Assert.Equal(operatorId, crete.OwningOperatorId);

        Assert.Equal(2, await db.SourceOffers.CountAsync());
    }

    [Fact]
    public async Task Rerun_isIdempotent_updatesInPlaceWithoutDuplicating()
    {
        await using var db = NewDb();
        var conn = Connection();

        await Connector(TwoValidOffers, db).RunAsync(conn, default);

        const string updated = """
        <offers>
          <offer>
            <externalId>crete-1</externalId>
            <title>Crete Sun Deluxe</title>
            <country>GR</country>
            <price>610</price>
            <board>AllInclusive</board>
            <transport>Plane</transport>
            <nights>7</nights>
          </offer>
        </offers>
        """;
        await Connector(updated, db).RunAsync(conn, default);

        var crete = await db.Offers.SingleAsync(o => o.Source!.ExternalId == "crete-1");
        Assert.Equal("Crete Sun Deluxe", crete.Title);
        Assert.Equal(610m, crete.Price);
        Assert.Equal(2, await db.Offers.CountAsync()); // no duplicate created
    }

    [Fact]
    public async Task InvalidRow_isSkipped_markedFailed_andRunStillSucceeds()
    {
        await using var db = NewDb();
        var conn = Connection();

        const string mixed = """
        <offers>
          <offer>
            <externalId>good-1</externalId><title>Good</title><country>GR</country>
            <price>100</price><board>HalfBoard</board><transport>Plane</transport><nights>5</nights>
          </offer>
          <offer>
            <externalId>bad-1</externalId><title>Bad</title><country>GR</country>
            <price>100</price><board>Caviar</board><transport>Plane</transport><nights>5</nights>
          </offer>
        </offers>
        """;
        var result = await Connector(mixed, db).RunAsync(conn, default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.OffersFetched);
        Assert.Equal(1, result.OffersImported);
        Assert.False(await db.Offers.AnyAsync(o => o.Source!.ExternalId == "bad-1"));

        var bad = await db.SourceOffers.SingleAsync(s => s.ExternalId == "bad-1");
        Assert.Equal(ImportState.Failed, bad.State);
    }

    [Fact]
    public async Task MalformedXml_returnsFailedRun_withoutThrowing()
    {
        await using var db = NewDb();
        var result = await Connector("<offers><offer></broken>", db).RunAsync(Connection(), default);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await db.Offers.ToListAsync());
    }

    [Fact]
    public async Task FetchFailure_returnsFailedRun_withoutThrowing()
    {
        await using var db = NewDb();
        var result = await Connector(null, db).RunAsync(Connection(), default);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Offers.ToListAsync());
    }

    [Fact]
    public async Task FieldMap_readsSupplierNamedElements_ontoCanonicalOffer()
    {
        await using var db = NewDb();
        var conn = new SupplierConnection
        {
            OperatorId = Guid.NewGuid(),
            Kind = SupplierConnectionKind.Xml,
            Name = "Mapped XML",
            ConfigJson = """
            {
              "url": "https://supplier.test/feed.xml",
              "fieldMap": {
                "offer": "package", "externalId": "ref", "title": "name",
                "country": "dest", "board": "mealPlan", "transport": "flight", "nights": "los"
              }
            }
            """,
        };

        // Row element is <package>, fields use supplier names.
        const string body = """
        <feed>
          <package>
            <ref>p-1</ref><name>Kos Family</name><dest>GR</dest>
            <price>520</price><mealPlan>AllInclusive</mealPlan><flight>Plane</flight><los>7</los>
          </package>
        </feed>
        """;
        var result = await Connector(body, db).RunAsync(conn, default);

        Assert.Equal(1, result.OffersImported);
        var offer = await db.Offers.SingleAsync();
        Assert.Equal("p-1", offer.Source!.ExternalId);
        Assert.Equal("Kos Family", offer.Title);
        Assert.Equal("GR", offer.Country);
        Assert.Equal(520m, offer.Price);
        Assert.Equal(BoardBasis.AllInclusive, offer.BoardBasis);
        Assert.Equal(7, offer.DurationNights);
    }
}
