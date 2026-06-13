using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.Infrastructure.Suppliers.Connectors;

namespace Odisea.UnitTests.Suppliers;

public class JsonApiConnectorTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    // Returns a fixed body for any request, or throws to simulate an unreachable host.
    private sealed class StubHandler(string? body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (body is null) throw new HttpRequestException("connection refused");
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private static HttpClient ClientReturning(string? body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new StubHandler(body, status));

    private static JsonApiConnector Connector(string? body, AppDbContext db) =>
        new(ClientReturning(body), new SourceOfferImporter(db));

    private static SupplierConnection Connection(Guid operatorId, string url = "https://supplier.test/offers") =>
        new()
        {
            OperatorId = operatorId,
            Kind = SupplierConnectionKind.JsonApi,
            Name = "JSON feed",
            ConfigJson = $$"""{"url":"{{url}}"}""",
        };

    private const string TwoValidOffers = """
    [
      { "externalId": "crete-1", "title": "Crete Sun", "country": "GR", "city": "Heraklion",
        "price": 540, "currency": "EUR", "board": "HalfBoard", "transport": "Plane", "nights": 7,
        "imageUrl": "https://img/1", "tags": ["beach","family"] },
      { "externalId": "antalya-1", "title": "Antalya AI", "country": "TR", "city": "Antalya",
        "price": 690, "board": "AllInclusive", "transport": "Plane", "nights": 7 }
    ]
    """;

    [Fact]
    public async Task Imports_validOffers_asPublishedOperatorOwnedSourcedOffers()
    {
        await using var db = NewDb();
        var operatorId = Guid.NewGuid();
        var conn = Connection(operatorId);
        var connector = Connector(TwoValidOffers, db);

        var result = await connector.RunAsync(conn, default);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.OffersFetched);
        Assert.Equal(2, result.OffersImported);

        var offers = await db.Offers.ToListAsync();
        Assert.Equal(2, offers.Count);
        Assert.All(offers, o =>
        {
            Assert.Equal(OfferStatus.Published, o.Status);
            Assert.Equal(OwnerType.Operator, o.OwnerType);
            Assert.Equal(operatorId, o.OwningOperatorId);
            Assert.Equal(conn.Id, o.Source!.SupplierConnectionId);
            Assert.Equal(ImportState.Imported, o.Source.ImportState);
        });

        var crete = offers.Single(o => o.Source!.ExternalId == "crete-1");
        Assert.Equal("Crete Sun", crete.Title);
        Assert.Equal(BoardBasis.HalfBoard, crete.BoardBasis);
        Assert.Equal(540m, crete.Price);
        Assert.Equal(["beach", "family"], crete.Tags);

        // SourceOffers recorded with raw payload + Imported state.
        var sources = await db.SourceOffers.ToListAsync();
        Assert.Equal(2, sources.Count);
        Assert.All(sources, s => Assert.Equal(ImportState.Imported, s.State));
        Assert.Contains(sources, s => s.RawPayload.Contains("crete-1"));
    }

    [Fact]
    public async Task Rerun_isIdempotent_updatesInPlaceWithoutDuplicating()
    {
        await using var db = NewDb();
        var conn = Connection(Guid.NewGuid());

        await Connector(TwoValidOffers, db).RunAsync(conn, default);

        const string updated = """
        [
          { "externalId": "crete-1", "title": "Crete Sun (updated)", "country": "GR", "city": "Chania",
            "price": 499, "board": "AllInclusive", "transport": "Plane", "nights": 7 }
        ]
        """;
        var result = await Connector(updated, db).RunAsync(conn, default);

        Assert.Equal(1, result.OffersImported);

        var crete = await db.Offers.SingleAsync(o => o.Source!.ExternalId == "crete-1");
        Assert.Equal("Crete Sun (updated)", crete.Title);
        Assert.Equal(499m, crete.Price);
        Assert.Equal(BoardBasis.AllInclusive, crete.BoardBasis);

        // Still exactly two offers total (crete updated, antalya untouched) and two source rows.
        Assert.Equal(2, await db.Offers.CountAsync());
        Assert.Equal(2, await db.SourceOffers.CountAsync());
    }

    [Fact]
    public async Task InvalidRow_isSkipped_markedFailed_andRunStillSucceeds()
    {
        await using var db = NewDb();
        var conn = Connection(Guid.NewGuid());

        const string mixed = """
        [
          { "externalId": "good-1", "title": "Good", "country": "GR", "price": 100,
            "board": "HalfBoard", "transport": "Plane", "nights": 5 },
          { "externalId": "bad-1", "title": "Bad board", "country": "GR", "price": 100,
            "board": "Caviar", "transport": "Plane", "nights": 5 }
        ]
        """;
        var result = await Connector(mixed, db).RunAsync(conn, default);

        Assert.True(result.Succeeded);          // bad row doesn't fail the whole run
        Assert.Equal(2, result.OffersFetched);
        Assert.Equal(1, result.OffersImported);

        Assert.Equal(1, await db.Offers.CountAsync());
        Assert.False(await db.Offers.AnyAsync(o => o.Source!.ExternalId == "bad-1"));

        // The bad row is still traceable as a Failed source record.
        var bad = await db.SourceOffers.SingleAsync(s => s.ExternalId == "bad-1");
        Assert.Equal(ImportState.Failed, bad.State);
    }

    [Fact]
    public async Task MissingUrl_returnsFailedRun()
    {
        await using var db = NewDb();
        var conn = new SupplierConnection
        {
            OperatorId = Guid.NewGuid(),
            Kind = SupplierConnectionKind.JsonApi,
            Name = "No url",
            ConfigJson = "{}",
        };

        var result = await Connector(TwoValidOffers, db).RunAsync(conn, default);

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Offers.ToListAsync());
    }

    [Fact]
    public async Task FetchFailure_returnsFailedRun_withoutThrowing()
    {
        await using var db = NewDb();
        var conn = Connection(Guid.NewGuid());

        var result = await Connector(null, db).RunAsync(conn, default);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Empty(await db.Offers.ToListAsync());
    }
}
