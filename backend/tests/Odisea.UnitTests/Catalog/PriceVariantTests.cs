using Microsoft.EntityFrameworkCore;
using Odisea.Application.Catalog.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;

namespace Odisea.UnitTests.Catalog;

public class PriceVariantTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    [Fact]
    public async Task PriceVariants_RoundTripThroughDb()
    {
        await using var db = NewDb();

        var offer = new Offer
        {
            Title = "Test", Country = "GR", City = "Athens",
            Price = 500m, Currency = "EUR",
            BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane,
            DurationNights = 7, Status = OfferStatus.Published,
        };
        db.Offers.Add(offer);
        db.PriceVariants.AddRange(
            new PriceVariant { OfferId = offer.Id, DepartureDate = new DateOnly(2026, 7, 1), DurationNights = 7, BoardBasis = BoardBasis.HalfBoard, Occupancy = 2, Price = 500m, Currency = "EUR" },
            new PriceVariant { OfferId = offer.Id, DepartureDate = new DateOnly(2026, 7, 15), DurationNights = 7, BoardBasis = BoardBasis.RoomOnly, Occupancy = 2, Price = 400m, Currency = "EUR" });
        await db.SaveChangesAsync();

        var loaded = await db.Offers
            .Include(o => o.PriceVariants)
            .FirstAsync(o => o.Id == offer.Id);

        Assert.Equal(2, loaded.PriceVariants.Count);
        Assert.Contains(loaded.PriceVariants, v => v.BoardBasis == BoardBasis.HalfBoard && v.Price == 500m);
        Assert.Contains(loaded.PriceVariants, v => v.BoardBasis == BoardBasis.RoomOnly && v.Price == 400m);
    }

    [Fact]
    public void OfferDto_ExposesPriceVariants_WhenIncluded()
    {
        var offer = new Offer
        {
            Title = "T", Country = "GR", City = "Athens",
            Price = 500m, Currency = "EUR",
            BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane,
            DurationNights = 7,
        };
        offer.PriceVariants.Add(new PriceVariant
        {
            DepartureDate = new DateOnly(2026, 7, 1),
            DurationNights = 7,
            BoardBasis = BoardBasis.AllInclusive,
            Occupancy = 2,
            Price = 700m,
            Currency = "EUR",
        });

        var dto = offer.ToDto();

        Assert.Single(dto.PriceVariants);
        var v = dto.PriceVariants[0];
        Assert.Equal(700m, v.Price);
        Assert.Equal("AllInclusive", v.BoardBasis);
        Assert.Equal(2, v.Occupancy);
    }

    [Fact]
    public void OfferDto_PriceVariantsEmpty_WhenNoneLoaded()
    {
        // The list endpoint doesn't Include() variants — DTO should still serialize
        // cleanly with an empty list rather than failing.
        var offer = new Offer
        {
            Title = "T", Country = "GR", City = "Athens",
            Price = 500m, Currency = "EUR",
            BoardBasis = BoardBasis.HalfBoard, Transport = Transport.Plane,
            DurationNights = 7,
        };

        var dto = offer.ToDto();

        Assert.Empty(dto.PriceVariants);
    }

    [Fact]
    public void PriceVariant_AllDimensionsNullable()
    {
        // Suppliers may publish a single price with no breakdown — every dimension
        // except Price + Currency must accept null without ceremony.
        var variant = new PriceVariant { Price = 999m, Currency = "EUR" };

        Assert.Null(variant.DepartureDate);
        Assert.Null(variant.DurationNights);
        Assert.Null(variant.BoardBasis);
        Assert.Null(variant.Occupancy);
    }
}
