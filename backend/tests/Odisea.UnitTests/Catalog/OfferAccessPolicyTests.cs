using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Access;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Catalog;

public class OfferAccessPolicyTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static Offer OperatorOffer(Guid operatorId, string title) => new()
    {
        Title = title, Country = "GR", City = "A", Price = 100,
        BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7,
        OwnerType = OwnerType.Operator, Visibility = Visibility.PlatformShared,
        OwningOperatorId = operatorId, Status = OfferStatus.Published,
    };

    private static Offer AgencyOffer(Guid agencyId, string title) => new()
    {
        Title = title, Country = "GR", City = "B", Price = 100,
        BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane, DurationNights = 7,
        OwnerType = OwnerType.Agency, Visibility = Visibility.AgencyPrivate,
        OwningAgencyId = agencyId, Status = OfferStatus.Published,
    };

    [Fact]
    public async Task EntitledAgency_SeesOperatorOffers()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        var op = Guid.NewGuid();
        db.Offers.Add(OperatorOffer(op, "Op offer"));
        db.Entitlements.Add(new OperatorAgencyEntitlement { OperatorId = op, AgencyId = agency, Status = EntitlementStatus.Active });
        await db.SaveChangesAsync();

        var q = await new OfferAccessPolicy(db).DistributableForAgencyAsync(agency, default);
        var offers = await q.ToListAsync();

        Assert.Single(offers);
        Assert.Equal("Op offer", offers[0].Title);
    }

    [Fact]
    public async Task NonEntitledAgency_DoesNotSeeOperatorOffers()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        var op = Guid.NewGuid();
        db.Offers.Add(OperatorOffer(op, "Op offer"));
        // No entitlement row.
        await db.SaveChangesAsync();

        var q = await new OfferAccessPolicy(db).DistributableForAgencyAsync(agency, default);
        Assert.Empty(await q.ToListAsync());
    }

    [Fact]
    public async Task SuspendedEntitlement_ExcludesOperatorOffers()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        var op = Guid.NewGuid();
        db.Offers.Add(OperatorOffer(op, "Op offer"));
        db.Entitlements.Add(new OperatorAgencyEntitlement { OperatorId = op, AgencyId = agency, Status = EntitlementStatus.Suspended });
        await db.SaveChangesAsync();

        var q = await new OfferAccessPolicy(db).DistributableForAgencyAsync(agency, default);
        Assert.Empty(await q.ToListAsync());
    }

    [Fact]
    public async Task OwnPrivateOffers_AlwaysVisible_EvenWithoutEntitlements()
    {
        await using var db = NewDb();
        var agency = Guid.NewGuid();
        db.Offers.Add(AgencyOffer(agency, "My private offer"));
        await db.SaveChangesAsync();

        var q = await new OfferAccessPolicy(db).DistributableForAgencyAsync(agency, default);
        var offers = await q.ToListAsync();

        Assert.Single(offers);
        Assert.Equal("My private offer", offers[0].Title);
    }

    [Fact]
    public async Task DoesNotLeakAnotherAgencysPrivateOffers()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        db.Offers.Add(AgencyOffer(theirs, "Their private offer"));
        await db.SaveChangesAsync();

        var q = await new OfferAccessPolicy(db).DistributableForAgencyAsync(mine, default);
        Assert.Empty(await q.ToListAsync());
    }
}
