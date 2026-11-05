using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Programs;
using CatalogProgram = Odisea.Modules.Catalog.Domain.Program;

namespace Odisea.UnitTests.Catalog;

/// Programs in the states the readiness and publishing tests care about.
public static class CatalogFixtures
{
    public static readonly DateOnly Today = new(2026, 10, 29);

    public static CatalogProgram Draft(string name = "Antalya Easter 2027") => new()
    {
        Name = name,
        Slug = Slug.From(name),
        DestinationId = Guid.NewGuid(),
        ProviderCode = "mock",
    };

    /// Everything a customer needs: content, an 8-day itinerary, one 2027 departure, one hotel.
    public static CatalogProgram Ready()
    {
        var program = Draft();
        program.Description = new string('x', 400);
        program.Content = FullContent();
        program.Departures.Add(new ProgramDeparture
        {
            ProgramId = program.Id,
            StartDate = new DateOnly(2027, 3, 30),
            EndDate = new DateOnly(2027, 4, 6),
            TransportNote = "Charter SOF-AYT",
        });
        program.Hotels.Add(new ProgramHotel { ProgramId = program.Id, HotelId = Guid.NewGuid() });
        for (var day = 1; day <= 8; day++)
            program.Itinerary.Add(new ProgramItineraryDay
            {
                ProgramId = program.Id,
                Day = day,
                Title = $"Day {day}",
                Meals = MealPlan.Breakfast | MealPlan.Dinner,
            });
        return program;
    }

    public static ProgramContent FullContent() => new()
    {
        Tagline = "7 nights all-inclusive",
        Summary = "A week of early-season sun with a direct charter, transfers and insurance all in one price.",
        Highlights = ["Direct charter", "Three hotels", "Transfers included"],
        Inclusions = ["Flights", "7 nights", "Transfers"],
        Exclusions = ["Excursions"],
        Tags = ["all-inclusive", "family"],
        TransportMode = TransportMode.Flight,
        TransportDetails = "Charter SOF-AYT, 20 kg baggage",
        DeparturePoint = "Sofia Airport (SOF)",
        PracticalInfo = "Passport valid 6 months",
        CancellationPolicy = "Free until 30 days before departure",
        PaymentTerms = "30% deposit",
        InsurancePolicy = "Medical insurance included",
        ChildPolicy = "Children 2–11 at the child rate",
    };

    public static SaveProgramContentRequest FullContentRequest(ItineraryDayRequest[]? itinerary = null, string? slug = null)
    {
        var c = FullContent();
        return new SaveProgramContentRequest(
            c.Tagline, c.Summary, c.Highlights, c.Inclusions, c.Exclusions, c.Tags,
            c.TransportMode, c.TransportDetails, c.DeparturePoint, null, null,
            c.PracticalInfo, c.CancellationPolicy, c.PaymentTerms, c.InsurancePolicy, c.ChildPolicy,
            itinerary ?? Days(8), slug);
    }

    public static ItineraryDayRequest[] Days(int count) =>
        [.. Enumerable.Range(1, count).Select(d => new ItineraryDayRequest(d, $"Day {d}", null, MealPlan.Breakfast, null))];

    public static List<ProgramImageInfo> Gallery(int count = 3, int coverWidth = 1600, bool altText = true) =>
        [.. Enumerable.Range(0, count).Select(i => new ProgramImageInfo(Guid.NewGuid(), i == 0, i == 0 ? coverWidth : 800, 900, altText))];
}
