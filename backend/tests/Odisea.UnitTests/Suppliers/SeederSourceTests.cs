using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Suppliers;

public class SeederSourceTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Seeder_creates_supplier_connection()
    {
        await using var db = CreateDb();
        await Seeder.SeedAsync(db);

        var connection = await db.SupplierConnections.SingleAsync();
        Assert.Equal(SupplierConnectionKind.Manual, connection.Kind);
        Assert.Equal(SupplierConnectionStatus.Active, connection.Status);
        Assert.Equal("Sun Operators (seeded)", connection.Name);
    }

    [Fact]
    public async Task Operator_offers_have_source_agency_private_do_not()
    {
        await using var db = CreateDb();
        await Seeder.SeedAsync(db);

        var connectionId = (await db.SupplierConnections.SingleAsync()).Id;
        var offers = await db.Offers.ToListAsync();

        var operatorOffers = offers.Where(o => o.OwnerType == OwnerType.Operator).ToList();
        Assert.NotEmpty(operatorOffers);
        Assert.All(operatorOffers, o =>
        {
            Assert.NotNull(o.Source);
            Assert.Equal(connectionId, o.Source!.SupplierConnectionId);
            Assert.False(string.IsNullOrWhiteSpace(o.Source.ExternalId));
            Assert.Equal(ImportState.Imported, o.Source.ImportState);
        });

        var agencyOffers = offers.Where(o => o.OwnerType == OwnerType.Agency).ToList();
        Assert.NotEmpty(agencyOffers);
        Assert.All(agencyOffers, o => Assert.Null(o.Source));
    }
}
