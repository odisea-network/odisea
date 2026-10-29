using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Infrastructure;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Pricing.Domain;
using Odisea.Modules.Pricing.Infrastructure;

namespace Odisea.Api;

/// Development-only demo data (guarded by Seed:DemoData) so a fresh
/// `docker compose up` + `dotnet run` gives a fully searchable, bookable
/// Antalya program in Swagger without any manual setup.
public static class DevDemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        if (!configuration.GetValue<bool>("Seed:DemoData"))
            return;

        // Each section guards itself, so new seed blocks still run on databases
        // that were seeded by an older version of this seeder.
        var catalog = services.GetRequiredService<CatalogDbContext>();
        if (!await catalog.Programs.AnyAsync())
            await SeedCatalogAsync(catalog);
        else
            await BackfillProgramContentAsync(catalog);

        await SeedPricingAsync(services);
        await SeedAgencyAsync(services);
        await SeedCompanyProfileAsync(services);

        logger.LogInformation("Demo seed verified: catalog, pricing, agency + billing, company profile");
    }

    private static async Task SeedCatalogAsync(CatalogDbContext catalog)
    {
        // --- Catalog: Antalya + three mapped hotels + a published Easter program
        var antalya = new Destination { Name = "Antalya", Country = "TR" };
        catalog.Destinations.Add(antalya);
        catalog.LocationMappings.Add(new LocationMapping
        {
            DestinationId = antalya.Id,
            ProviderCode = MockReservationProvider.Code,
            ExternalLocationCode = "ANT",
        });

        (string Name, int Stars, string Ext)[] hotelSeed =
        [
            ("Mock Palace Resort", 5, "MOCK-ANT-001"),
            ("Mock Garden Hotel", 4, "MOCK-ANT-002"),
            ("Mock Beach Club", 3, "MOCK-ANT-003"),
        ];
        List<Hotel> hotels = [];
        foreach (var (name, stars, ext) in hotelSeed)
        {
            var hotel = new Hotel { Name = name, DestinationId = antalya.Id, Stars = stars };
            hotels.Add(hotel);
            catalog.Hotels.Add(hotel);
            catalog.HotelMappings.Add(new HotelMapping
            {
                HotelId = hotel.Id,
                ProviderCode = MockReservationProvider.Code,
                ExternalHotelCode = ext,
            });
        }

        var program = new Odisea.Modules.Catalog.Domain.Program
        {
            Name = DemoProgram.Name,
            Slug = DemoProgram.Slug,
            DestinationId = antalya.Id,
            ProviderCode = MockReservationProvider.Code,
            Season = "Easter 2027",
            Description = DemoProgram.Description,
            Content = DemoProgram.Content(),
        };
        program.Departures.Add(new ProgramDeparture
        {
            ProgramId = program.Id,
            StartDate = new DateOnly(2027, 3, 30),
            EndDate = new DateOnly(2027, 4, 6),
            TransportNote = "Charter SOF-AYT",
        });
        foreach (var hotel in hotels)
            program.Hotels.Add(new ProgramHotel { ProgramId = program.Id, HotelId = hotel.Id });
        program.Itinerary.AddRange(DemoProgram.Itinerary(program.Id));
        var images = DemoProgram.Images(program.Id).ToList();
        catalog.ProgramImages.AddRange(images);

        program.Publish(ProgramReadiness.Evaluate(
            program,
            [.. images.Select(i => new ProgramImageInfo(i.Id, i.IsCover, i.Width, i.Height, i.AltText is not null))],
            mappedHotels: hotels.Count,
            DateOnly.FromDateTime(DateTime.UtcNow)));
        catalog.Programs.Add(program);
        await catalog.SaveChangesAsync();
    }

    /// Gives the demo program seeded before content existed its text, itinerary and pictures.
    private static async Task BackfillProgramContentAsync(CatalogDbContext catalog)
    {
        var program = await catalog.Programs
            .Include(p => p.Itinerary)
            .FirstOrDefaultAsync(p => p.Name == DemoProgram.Name && p.Content.Summary == null);
        if (program is null)
            return;

        program.Content = DemoProgram.Content();
        program.Description = DemoProgram.Description;
        if (!await catalog.Programs.AnyAsync(p => p.Slug == DemoProgram.Slug))
            program.Slug = DemoProgram.Slug;
        if (program.Itinerary.Count == 0)
            catalog.ProgramItineraryDays.AddRange(DemoProgram.Itinerary(program.Id));
        if (!await catalog.ProgramImages.AnyAsync(i => i.ProgramId == program.Id))
            catalog.ProgramImages.AddRange(DemoProgram.Images(program.Id));
        await catalog.SaveChangesAsync();
    }

    private static async Task SeedPricingAsync(IServiceProvider services)
    {
        // --- Pricing: global markup + fee + commission
        var pricing = services.GetRequiredService<PricingDbContext>();
        if (!await pricing.PricingRules.AnyAsync())
        {
            pricing.PricingRules.AddRange(
                new PricingRule { Name = "Global markup 15%", Kind = RuleKind.Markup, ValueType = RuleValueType.Percent, Value = 15m },
                new PricingRule { Name = "Service fee", Kind = RuleKind.Fee, ValueType = RuleValueType.FixedAmount, Value = 10m, Priority = 0 },
                new PricingRule { Name = "Standard commission 10%", Kind = RuleKind.Commission, ValueType = RuleValueType.Percent, Value = 10m });
            await pricing.SaveChangesAsync();
        }
    }

    private static async Task SeedAgencyAsync(IServiceProvider services)
    {
        // --- Agencies: a demo agency + agent login for the B2B side
        var agencies = services.GetRequiredService<AgenciesDbContext>();
        if (!await agencies.AgencyUsers.AnyAsync())
        {
            var agency = new Agency
            {
                Name = "Blue Horizon Travel",
                Country = "BG",
                // Workspace so the demo agency can use every agency feature out of the box.
                Plan = AgencyPlan.Workspace,
                CreditLimit = 50_000m,
                ContactEmail = "office@bluehorizon.example",
                // Demo billing requisites so invoices are issuable out of the box.
                LegalName = "Блу Хоризон Травел ЕООД",
                Eik = "201234567",
                VatNumber = "BG201234567",
                BillingAddress = "ул. Демонстрационна 1",
                BillingCity = "София",
                Mol = "Демо Управител",
            };
            agencies.Agencies.Add(agency);

            var hasher = services.GetRequiredService<IPasswordHasher<UserAccount>>();
            var agent = new AgencyUser
            {
                AgencyId = agency.Id,
                Email = "agent@odisea.local",
                PasswordHash = "",
                FullName = "Demo Agent",
                Role = AgencyUserRole.AgencyAdmin,
            };
            agent.PasswordHash = hasher.HashPassword(agent, "DevOnly-Odisea-Agent-2026!");
            agencies.AgencyUsers.Add(agent);
            await agencies.SaveChangesAsync();
        }
        else
        {
            // Backfill billing requisites onto a demo agency seeded before they existed.
            var demoAgentAgencyId = await agencies.AgencyUsers
                .Where(u => u.Email == "agent@odisea.local")
                .Select(u => u.AgencyId)
                .FirstOrDefaultAsync();
            var agency = await agencies.Agencies.FindAsync(demoAgentAgencyId);
            if (agency is { Eik: null })
            {
                agency.LegalName = "Блу Хоризон Травел ЕООД";
                agency.Eik = "201234567";
                agency.VatNumber = "BG201234567";
                agency.BillingAddress = "ул. Демонстрационна 1";
                agency.BillingCity = "София";
                agency.Mol = "Демо Управител";
                await agencies.SaveChangesAsync();
            }
        }
    }

    private static async Task SeedCompanyProfileAsync(IServiceProvider services)
    {
        // --- Documents: demo issuer profile (placeholders — replace with real company data)
        var documents = services.GetRequiredService<Odisea.Modules.Documents.Infrastructure.DocumentsDbContext>();
        if (!await documents.CompanyProfiles.AnyAsync())
        {
            documents.CompanyProfiles.Add(new Odisea.Modules.Documents.Domain.CompanyProfile
            {
                Name = "Одисея Травел ЕООД (демо)",
                Eik = "209876543",
                VatNumber = "BG209876543",
                Address = "бул. Витоша 100",
                City = "София",
                Mol = "Демо Директор",
                Iban = "BG80BNBG96611020345678",
                BankName = "Демо Банка АД",
            });
            await documents.SaveChangesAsync();
        }
    }
}
