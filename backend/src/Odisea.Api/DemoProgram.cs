using Odisea.Modules.Catalog.Domain;

namespace Odisea.Api;

/// The demo Antalya program's sell-ready content — what a finished program looks like, and the
/// reference the create-program skill's example is drawn from.
public static class DemoProgram
{
    public const string Name = "Antalya Easter 2027";
    public const string Slug = "antalya-easter-2027";

    public const string Description = """
        ## A week on the Turkish Riviera

        Seven nights in Antalya over the Easter holidays, when the sea is warming up and the resorts are at their quietest. The package puts together a charter flight from Sofia, transfers on both ends, a choice of three all-inclusive hotels on the Lara and Belek coast, and medical travel insurance, so the only decision left is which pool to sit by.

        ### Who it is for

        Families with school-age children, couples who want a week of guaranteed sun before the summer rush, and groups travelling together who want everything handled by one operator.

        ### How it works

        Choose the departure and the hotel at booking time. Prices come live from the hotel system and already include transport, transfers and insurance. Seats on the charter are limited and allocated in booking order.
        """;

    public static ProgramContent Content() => new()
    {
        Tagline = "7 nights all-inclusive with a direct charter from Sofia",
        Summary = "A week of early-season sun in Antalya: direct charter from Sofia, transfers, medical insurance and "
                  + "a choice of three all-inclusive hotels on the Lara and Belek coast, all in one price.",
        Highlights =
        [
            "Direct charter Sofia → Antalya, about 1 h 45 min in the air",
            "Three hand-picked all-inclusive hotels on the Lara and Belek coast",
            "Festive Easter-week programme and themed dinners at every hotel",
            "Transfers, insurance and resort taxes already in the price",
            "Optional excursions: Perge and Aspendos, Düden waterfalls, Antalya old town",
        ],
        Inclusions =
        [
            "Return charter flight Sofia (SOF) – Antalya (AYT) with 20 kg checked baggage and 8 kg hand luggage",
            "7 nights at the chosen hotel on an all-inclusive basis",
            "Group transfers airport – hotel – airport",
            "Medical travel insurance for the duration of the trip",
            "Bulgarian-speaking representative at the resort",
            "Airport taxes and fuel surcharge",
        ],
        Exclusions =
        [
            "Optional excursions and entrance fees",
            "Personal expenses, mini-bar and spa treatments",
            "Trip-cancellation insurance (available on request)",
            "Seat selection and priority boarding",
            "Visa, where required by the traveller's nationality",
        ],
        Tags = ["all-inclusive", "family", "beach", "easter", "charter"],
        TransportMode = TransportMode.Flight,
        TransportDetails = """
            Charter flight operated by a Bulgarian carrier.

            - **Outbound:** Sofia (SOF) 06:30 → Antalya (AYT) 09:15 local time
            - **Return:** Antalya (AYT) 10:30 → Sofia (SOF) 11:20 local time
            - 20 kg checked baggage and 8 kg hand luggage per person
            - Flight times are indicative and confirmed 48 hours before departure
            """,
        DeparturePoint = "Sofia Airport (SOF), Terminal 2",
        PracticalInfo = """
            - **Documents:** a passport or Bulgarian ID card valid for at least 6 months after the return date. Bulgarian citizens do not need a visa for stays of up to 90 days.
            - **Currency:** Turkish lira; euros and cards are accepted in the resorts.
            - **Weather:** 20–24 °C by day in early April, sea around 18 °C; evenings are cool, bring a light jacket.
            - **Time zone:** Turkey is one hour ahead of Bulgaria.
            - **Health:** no vaccinations required; tap water is not for drinking.
            """,
        CancellationPolicy = """
            - More than 30 days before departure: full refund minus a 50 EUR per person processing fee.
            - 30 to 15 days before departure: 50% of the package price is retained.
            - 14 to 8 days before departure: 75% is retained.
            - 7 days or less, or no-show: 100% is retained.

            Name changes are free up to 7 days before departure, subject to the carrier's rules.
            """,
        PaymentTerms = "30% deposit within 3 working days of confirmation; the balance is due 21 days before departure. "
                       + "Bookings made within 21 days of departure are paid in full at confirmation. Bank transfer or card.",
        InsurancePolicy = "Medical travel insurance with 10 000 EUR cover for emergency treatment, hospitalisation and "
                          + "repatriation is included for every traveller. Trip-cancellation cover is optional and must be "
                          + "added at the time of booking. Pre-existing conditions are excluded unless declared and accepted "
                          + "by the insurer.",
        ChildPolicy = "Children aged 2–11 sharing a room with two adults travel at the child rate shown at booking. "
                      + "Infants under 2 pay only the airline fee and do not get a seat. Ages count on the return date.",
    };

    public static IEnumerable<ProgramItineraryDay> Itinerary(Guid programId)
    {
        (string Title, string Description, MealPlan Meals, string? Overnight)[] days =
        [
            ("Arrival in Antalya",
             "Morning charter from Sofia. Group transfer to the hotel (about 30 minutes), check-in from 14:00, welcome briefing with the representative at 18:00.",
             MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Beach day",
             "A free day at the hotel. The representative is at the lobby desk 10:00–12:00 to book excursions.",
             MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Antalya old town (optional)",
             "Half-day walk through Kaleiçi: Hadrian's Gate, the clock tower, the old harbour and the bazaar. Return by 15:00.",
             MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Perge and Aspendos (optional)",
             "Full-day excursion to the Roman city of Perge and the theatre of Aspendos, with lunch in Side.",
             MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Festive day at the resort",
             "Themed lunch and an evening show at the hotel. Pools, spa and the beach as usual.",
             MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Düden waterfalls and Lara beach (optional)",
             "Half-day trip to the upper and lower Düden waterfalls, then free time on Lara beach.",
             MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Free day",
             "Last full day at the resort. Return flight times are confirmed by the representative in the evening.",
             MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            ("Return to Sofia",
             "Check-out by 12:00 (late check-out subject to availability), transfer to the airport and the midday flight home.",
             MealPlan.Breakfast, null),
        ];
        return days.Select((d, i) => new ProgramItineraryDay
        {
            ProgramId = programId,
            Day = i + 1,
            Title = d.Title,
            Description = d.Description,
            Meals = d.Meals,
            Overnight = d.Overnight,
        });
    }

    public static IEnumerable<ProgramImage> Images(Guid programId)
    {
        (string Caption, string Alt, (byte, byte, byte) Top, (byte, byte, byte) Bottom)[] pictures =
        [
            ("Lara beach at sunrise", "Sandy beach with a calm sea at sunrise", (33, 110, 170), (150, 205, 230)),
            ("Pool area at a Belek resort", "Resort pool surrounded by palm trees", (230, 170, 90), (250, 225, 180)),
            ("Theatre of Aspendos", "Stone amphitheatre ruins under a blue sky", (90, 120, 70), (200, 190, 150)),
        ];
        return pictures.Select((p, i) =>
        {
            var bytes = DemoImages.Png(1600, 900, p.Top, p.Bottom);
            return new ProgramImage
            {
                ProgramId = programId,
                ContentType = "image/png",
                Data = bytes,
                SizeBytes = bytes.Length,
                Width = 1600,
                Height = 900,
                Sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
                Caption = p.Caption,
                AltText = p.Alt,
                SortOrder = i,
                IsCover = i == 0,
            };
        });
    }
}
