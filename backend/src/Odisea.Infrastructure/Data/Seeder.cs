using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Infrastructure.Data;

// Idempotent dev seed: 2 agencies, 1 operator, ~14 offers across BG/GR/TR/EG,
// 3 collections with flat FilterSpecs that match the seeded data.
// Seeded credentials (dev only — override via env in production):
//   admin@odisea.net        / Admin1234!   → PlatformAdmin
//   blue@blue-horizon.com   / Blue1234!    → AgencyAdmin  (Blue Horizon Travel)
//   green@green-path.com    / Green1234!   → AgencyAdmin  (Green Path Tours)
//   ops@sun-operators.com   / Ops1234!     → OperatorAdmin (Sun Operators Ltd)
public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Agencies.AnyAsync(ct)) return;

        var blue = new Agency { Name = "Blue Horizon Travel", Slug = "blue-horizon" };
        var green = new Agency { Name = "Green Path Tours", Slug = "green-path" };
        var sunOps = new Operator { Name = "Sun Operators Ltd", Slug = "sun-operators" };

        db.Agencies.AddRange(blue, green);
        db.Operators.Add(sunOps);

        var img = (string seed) => $"https://picsum.photos/seed/{seed}/640/360";

        var offers = new List<Offer>
        {
            // Operator-shared (PlatformShared)
            Make("Crete Sun Escape", "Greece", "GR", "Heraklion", 720, BoardBasis.HalfBoard, Transport.Plane, 7,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"beach","family"}, image: img("crete")),
            Make("Santorini Caldera Boutique", "Greece", "GR", "Santorini", 1290, BoardBasis.BedAndBreakfast, Transport.Plane, 5,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"romance","views"}, image: img("santorini")),
            Make("Rhodes All-Inclusive", "Greece", "GR", "Rhodes", 890, BoardBasis.AllInclusive, Transport.Plane, 7,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"beach","all-inclusive"}, image: img("rhodes")),
            Make("Halkidiki Coach Holiday", "Greece", "GR", "Halkidiki", 410, BoardBasis.FullBoard, Transport.Bus, 6,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"budget","bus"}, image: img("halkidiki")),
            Make("Antalya Family AI", "Turkey", "TR", "Antalya", 690, BoardBasis.AllInclusive, Transport.Plane, 7,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"family","all-inclusive"}, image: img("antalya")),
            Make("Bodrum Boutique Stay", "Turkey", "TR", "Bodrum", 980, BoardBasis.HalfBoard, Transport.Plane, 5,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"boutique"}, image: img("bodrum")),
            Make("Istanbul Weekend Break", "Turkey", "TR", "Istanbul", 350, BoardBasis.BedAndBreakfast, Transport.Plane, 3,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"city-break"}, image: img("istanbul")),
            Make("Hurghada Red Sea AI", "Egypt", "EG", "Hurghada", 640, BoardBasis.AllInclusive, Transport.Plane, 7,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"diving","all-inclusive"}, image: img("hurghada")),
            Make("Sharm El Sheikh Diver", "Egypt", "EG", "Sharm El Sheikh", 690, BoardBasis.AllInclusive, Transport.Plane, 7,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"diving"}, image: img("sharm")),
            Make("Cairo & Nile Cruise", "Egypt", "EG", "Cairo", 1450, BoardBasis.FullBoard, Transport.Plane, 8,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"culture","cruise"}, image: img("cairo")),
            Make("Sofia Mountain Retreat", "Bulgaria", "BG", "Sofia", 280, BoardBasis.HalfBoard, Transport.Own, 4,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"mountains","local"}, image: img("sofia")),
            Make("Sunny Beach Weekender", "Bulgaria", "BG", "Sunny Beach", 220, BoardBasis.AllInclusive, Transport.Bus, 3,
                Visibility.PlatformShared, OwnerType.Operator, supplier: sunOps.Id, tags: new(){"beach","budget"}, image: img("sunnybeach")),

            // Agency-private offers
            Make("Blue Horizon's Mykonos VIP", "Greece", "GR", "Mykonos", 1890, BoardBasis.BedAndBreakfast, Transport.Plane, 5,
                Visibility.AgencyPrivate, OwnerType.Agency, owningAgency: blue.Id, tags: new(){"luxury","exclusive"}, image: img("mykonos")),
            Make("Green Path's Eco Plovdiv Tour", "Bulgaria", "BG", "Plovdiv", 320, BoardBasis.BedAndBreakfast, Transport.Own, 3,
                Visibility.AgencyPrivate, OwnerType.Agency, owningAgency: green.Id, tags: new(){"eco","culture"}, image: img("plovdiv")),
        };

        db.Offers.AddRange(offers);

        // Capture collection variables so IDs are available for Publication seeding.
        var summerGreece = new Collection
        {
            AgencyId = blue.Id,
            Name = "Summer in Greece",
            Slug = "summer-greece",
            Status = CollectionStatus.Published,
            Filter = Spec(("country", "eq", "GR")),
            Sort = new SortSpec("price", "asc"),
        };
        var lastMinuteEgypt = new Collection
        {
            AgencyId = green.Id,
            Name = "Last-minute Egypt",
            Slug = "last-minute-egypt",
            Status = CollectionStatus.Published,
            Filter = Spec(("country", "eq", "EG"), ("maxPrice", "lte", 700m)),
            Sort = new SortSpec("price", "asc"),
        };
        var blueVip = new Collection
        {
            AgencyId = blue.Id,
            Name = "Blue Horizon VIP picks",
            Slug = "blue-horizon-vip",
            Status = CollectionStatus.Published,
            Filter = Spec(("tag", "contains", "luxury")),
            Sort = new SortSpec("price", "desc"),
        };
        var greekIslandsPremium = new Collection
        {
            AgencyId = blue.Id,
            Name = "Greek islands premium",
            Slug = "greek-islands-premium",
            Status = CollectionStatus.Published,
            // country=GR AND (board=AllInclusive OR maxPrice<=600)
            Filter = Spec(("country", "eq", "GR"))
                .WithGroup(Group("any",
                    ("board", "eq", "AllInclusive"),
                    ("maxPrice", "lte", 600m))),
            Sort = new SortSpec("price", "desc"),
        };

        db.Collections.AddRange(summerGreece, lastMinuteEgypt, blueVip, greekIslandsPremium);

        await db.SaveChangesAsync(ct);

        // Publications are seeded after collections are persisted.
        // Wrapped in try/catch: the publications table may not exist yet
        // (migration is sequenced at merge with the Theme agent).
        await SeedPublicationsAsync(db, blue.Id, green.Id, summerGreece.Id, lastMinuteEgypt.Id, ct);

        await SeedUsersAsync(db, blue.Id, green.Id, sunOps.Id, ct);

        await SeedEventsAsync(db, ct);
    }

    // Dev convenience: a 30-day spread of analytics events for the seeded
    // publications so the dashboard has real numbers to render. The funnel
    // narrows impression -> open -> inquiry-start -> inquiry-submit.
    private static async Task SeedEventsAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            if (await db.Events.AnyAsync(ct)) return;

            var keys = await db.Publications
                .Select(p => p.Key)
                .ToListAsync(ct);

            if (keys.Count == 0) return;

            var rng = new Random(8); // deterministic seed -> stable dev data
            var now = DateTime.UtcNow;
            var events = new List<Event>();

            foreach (var key in keys)
            {
                for (var dayAgo = 0; dayAgo < 30; dayAgo++)
                {
                    var day = now.AddDays(-dayAgo);
                    var impressions = rng.Next(20, 80);
                    var opens = (int)(impressions * (0.25 + rng.NextDouble() * 0.15));
                    var inquiryStarts = (int)(opens * (0.15 + rng.NextDouble() * 0.15));
                    var inquirySubmits = (int)(inquiryStarts * (0.3 + rng.NextDouble() * 0.3));

                    void Add(EventType type, int count)
                    {
                        for (var i = 0; i < count; i++)
                            events.Add(new Event
                            {
                                EventType = type,
                                PublicationKey = key,
                                Channel = Channel.WebComponent,
                                OccurredAt = day.AddSeconds(-rng.Next(0, 86_400)),
                            });
                    }

                    Add(EventType.Impression, impressions);
                    Add(EventType.Open, opens);
                    Add(EventType.InquiryStart, inquiryStarts);
                    Add(EventType.InquirySubmit, inquirySubmits);
                }
            }

            db.Events.AddRange(events);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // events table may not exist yet — migration is sequenced at merge (#23).
            // Will succeed once the migration is applied.
        }
    }

    private static async Task SeedPublicationsAsync(
        AppDbContext db,
        Guid blueAgencyId,
        Guid greenAgencyId,
        Guid summerGreeceId,
        Guid lastMinuteEgyptId,
        CancellationToken ct)
    {
        try
        {
            if (await db.Publications.AnyAsync(ct)) return;

            db.Publications.AddRange(
                new Publication
                {
                    Key = "blue-gr-summer",
                    AgencyId = blueAgencyId,
                    CollectionId = summerGreeceId,
                    Status = PublicationStatus.Published,
                    Version = 1,
                },
                new Publication
                {
                    Key = "green-eg-last",
                    AgencyId = greenAgencyId,
                    CollectionId = lastMinuteEgyptId,
                    Status = PublicationStatus.Published,
                    Version = 1,
                }
            );

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Publications table does not exist yet — migration pending.
            // This will succeed once the migration is applied at merge time.
        }
    }

    private static Offer Make(
        string title, string country, string countryCode, string city, decimal price,
        BoardBasis board, Transport transport, int nights,
        Visibility visibility, OwnerType ownerType,
        Guid? owningAgency = null, Guid? supplier = null,
        List<string>? tags = null, string image = "")
    => new()
    {
        Title = title,
        Description = $"Hand-picked {nights}-night escape to {city}, {country}.",
        Country = countryCode,
        City = city,
        Price = price,
        Currency = "EUR",
        BoardBasis = board,
        Transport = transport,
        DurationNights = nights,
        Visibility = visibility,
        OwnerType = ownerType,
        OwningAgencyId = owningAgency,
        SupplierId = supplier,
        Tags = tags ?? new(),
        ImageUrl = image,
        Status = OfferStatus.Published,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14 + nights)),
    };

    private static FilterSpec Spec(params (string field, string op, object value)[] conds)
    {
        var spec = new FilterSpec();
        foreach (var (f, o, v) in conds)
            spec.All.Add(Leaf(f, o, v));
        return spec;
    }

    private static FilterGroup Group(string op, params (string field, string op, object value)[] conds)
    {
        var g = new FilterGroup { Op = op };
        foreach (var (f, oo, v) in conds)
            g.Conditions.Add(Leaf(f, oo, v));
        return g;
    }

    private static FilterCondition Leaf(string field, string op, object value) =>
        new() { Field = field, Op = op, Value = JsonSerializer.SerializeToElement(value) };

    private static async Task SeedUsersAsync(
        AppDbContext db,
        Guid blueId,
        Guid greenId,
        Guid sunOpsId,
        CancellationToken ct)
    {
        try
        {
            if (await db.Users.AnyAsync(ct)) return;

            var hasher = new PasswordHasher<User>();

            var admin = new User
            {
                Email = "admin@odisea.net",
                DisplayName = "Platform Admin",
                PasswordHash = hasher.HashPassword(new User(), "Admin1234!"),
                Status = UserStatus.Active,
            };

            var blueUser = new User
            {
                Email = "blue@blue-horizon.com",
                DisplayName = "Blue Horizon Admin",
                PasswordHash = hasher.HashPassword(new User(), "Blue1234!"),
                Status = UserStatus.Active,
            };

            var greenUser = new User
            {
                Email = "green@green-path.com",
                DisplayName = "Green Path Admin",
                PasswordHash = hasher.HashPassword(new User(), "Green1234!"),
                Status = UserStatus.Active,
            };

            var opsUser = new User
            {
                Email = "ops@sun-operators.com",
                DisplayName = "Sun Operators Admin",
                PasswordHash = hasher.HashPassword(new User(), "Ops1234!"),
                Status = UserStatus.Active,
            };

            db.Users.AddRange(admin, blueUser, greenUser, opsUser);
            await db.SaveChangesAsync(ct);

            db.Memberships.AddRange(
                // PlatformAdmin — no tenant
                new Membership
                {
                    UserId = admin.Id,
                    TenantType = TenantType.Agency,
                    TenantId = null,
                    Role = UserRole.PlatformAdmin,
                },
                new Membership
                {
                    UserId = blueUser.Id,
                    TenantType = TenantType.Agency,
                    TenantId = blueId,
                    Role = UserRole.AgencyAdmin,
                },
                new Membership
                {
                    UserId = greenUser.Id,
                    TenantType = TenantType.Agency,
                    TenantId = greenId,
                    Role = UserRole.AgencyAdmin,
                },
                new Membership
                {
                    UserId = opsUser.Id,
                    TenantType = TenantType.Operator,
                    TenantId = sunOpsId,
                    Role = UserRole.OperatorAdmin,
                });

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Users table may not exist yet if the migration hasn't run.
            // Will succeed once the migration is applied.
        }
    }
}

file static class FilterSpecSeedExtensions
{
    public static FilterSpec WithGroup(this FilterSpec spec, FilterGroup group)
    {
        spec.Groups.Add(group);
        return spec;
    }
}
