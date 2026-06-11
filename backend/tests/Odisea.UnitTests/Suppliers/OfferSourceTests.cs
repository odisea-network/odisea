using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Suppliers;

public class OfferSourceTests
{
    // A single named in-memory store so writes and reads share state across contexts.
    private static DbContextOptions<AppDbContext> SharedStore() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    [Fact]
    public async Task Offer_with_source_round_trips()
    {
        var options = SharedStore();
        var connectionId = Guid.NewGuid();
        var offerId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            db.Offers.Add(new Offer
            {
                Id = offerId,
                Title = "Crete Sun Escape",
                Country = "GR",
                City = "Heraklion",
                Currency = "EUR",
                Source = new OfferSource
                {
                    SupplierConnectionId = connectionId,
                    ExternalId = "crete-sun-escape",
                    ImportState = ImportState.Imported,
                    LastImportedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                },
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var loaded = await db.Offers.SingleAsync(o => o.Id == offerId);

            Assert.NotNull(loaded.Source);
            Assert.Equal(connectionId, loaded.Source!.SupplierConnectionId);
            Assert.Equal("crete-sun-escape", loaded.Source.ExternalId);
            Assert.Equal(ImportState.Imported, loaded.Source.ImportState);
            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), loaded.Source.LastImportedAt);
        }
    }

    [Fact]
    public async Task Offer_without_source_is_null()
    {
        var options = SharedStore();
        var offerId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            db.Offers.Add(new Offer { Id = offerId, Title = "Private", Country = "GR", City = "Mykonos", Currency = "EUR" });
            await db.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var loaded = await db.Offers.SingleAsync(o => o.Id == offerId);
            Assert.Null(loaded.Source);
        }
    }
}
