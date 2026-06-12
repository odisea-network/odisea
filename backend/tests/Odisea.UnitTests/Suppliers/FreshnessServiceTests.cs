using Microsoft.EntityFrameworkCore;
using Odisea.Application.Suppliers.Freshness;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Suppliers;

public class FreshnessServiceTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static async Task<SupplierConnection> SeedConnection(AppDbContext db, int ttlHours)
    {
        var op = new Operator { Name = "Op", Slug = "op" };
        db.Operators.Add(op);
        var conn = new SupplierConnection
        {
            OperatorId = op.Id, Kind = SupplierConnectionKind.Xml, Name = "Conn",
            FreshnessTtlHours = ttlHours,
        };
        db.SupplierConnections.Add(conn);
        await db.SaveChangesAsync();
        return conn;
    }

    [Fact]
    public async Task Sweep_MarksStaleSourceOffers_AndPropagatesToOffers()
    {
        await using var db = NewDb();
        var conn = await SeedConnection(db, ttlHours: 24);
        var now = DateTime.UtcNow;

        // One stale source (seen 48h ago), one fresh (seen 1h ago).
        db.SourceOffers.AddRange(
            new SourceOffer { SupplierConnectionId = conn.Id, ExternalId = "ext-stale", State = ImportState.Imported, LastSeenAt = now.AddHours(-48) },
            new SourceOffer { SupplierConnectionId = conn.Id, ExternalId = "ext-fresh", State = ImportState.Imported, LastSeenAt = now.AddHours(-1) });

        // Normalized offers carrying the same source external ids.
        db.Offers.AddRange(
            new Offer { Title = "Stale", Country = "GR", City = "A", Price = 100, BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7,
                Source = new OfferSource { SupplierConnectionId = conn.Id, ExternalId = "ext-stale", ImportState = ImportState.Imported } },
            new Offer { Title = "Fresh", Country = "GR", City = "B", Price = 100, BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7,
                Source = new OfferSource { SupplierConnectionId = conn.Id, ExternalId = "ext-fresh", ImportState = ImportState.Imported } });
        await db.SaveChangesAsync();

        var result = await new FreshnessService(db).SweepAsync(conn.Id, default);

        Assert.Equal(1, result.SourceOffersMarkedStale);
        Assert.Equal(1, result.OffersMarkedStale);

        var staleSource = await db.SourceOffers.FirstAsync(s => s.ExternalId == "ext-stale");
        var freshSource = await db.SourceOffers.FirstAsync(s => s.ExternalId == "ext-fresh");
        Assert.Equal(ImportState.Stale, staleSource.State);
        Assert.Equal(ImportState.Imported, freshSource.State);

        var staleOffer = await db.Offers.FirstAsync(o => o.Title == "Stale");
        var freshOffer = await db.Offers.FirstAsync(o => o.Title == "Fresh");
        Assert.Equal(ImportState.Stale, staleOffer.Source!.ImportState);
        Assert.Equal(ImportState.Imported, freshOffer.Source!.ImportState);
    }

    [Fact]
    public async Task Sweep_IsIdempotent_SecondRunMarksNothing()
    {
        await using var db = NewDb();
        var conn = await SeedConnection(db, ttlHours: 24);

        db.SourceOffers.Add(new SourceOffer
        {
            SupplierConnectionId = conn.Id, ExternalId = "x", State = ImportState.Imported,
            LastSeenAt = DateTime.UtcNow.AddHours(-48),
        });
        await db.SaveChangesAsync();

        var svc = new FreshnessService(db);
        var first = await svc.SweepAsync(conn.Id, default);
        var second = await svc.SweepAsync(conn.Id, default);

        Assert.Equal(1, first.SourceOffersMarkedStale);
        Assert.Equal(0, second.SourceOffersMarkedStale);
    }

    [Fact]
    public async Task Sweep_RespectsPerConnectionTtl()
    {
        await using var db = NewDb();
        // 72h TTL — a record seen 48h ago is still fresh.
        var conn = await SeedConnection(db, ttlHours: 72);

        db.SourceOffers.Add(new SourceOffer
        {
            SupplierConnectionId = conn.Id, ExternalId = "x", State = ImportState.Imported,
            LastSeenAt = DateTime.UtcNow.AddHours(-48),
        });
        await db.SaveChangesAsync();

        var result = await new FreshnessService(db).SweepAsync(conn.Id, default);

        Assert.Equal(0, result.SourceOffersMarkedStale);
    }

    [Fact]
    public async Task Sweep_UnknownConnection_Throws()
    {
        await using var db = NewDb();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new FreshnessService(db).SweepAsync(Guid.NewGuid(), default));
    }
}
