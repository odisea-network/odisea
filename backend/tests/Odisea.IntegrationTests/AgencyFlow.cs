using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Booking.Domain;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Catalog.Features.Programs;
using Odisea.Modules.Catalog.Features.Search;

namespace Odisea.IntegrationTests;

/// The agency-side happy path over HTTP, reusable by every test that needs a booking.
public static class AgencyFlow
{
    /// Two adults with everything a provider needs; the lead carries a passport when asked.
    public static PassengerRequest[] TwoAdults(string? leadDocumentNumber = null) =>
    [
        new PassengerRequest("Ivan", "Petrov", new DateOnly(1985, 5, 1),
            Gender: Gender.Male, Nationality: "BG",
            DocumentType: leadDocumentNumber is null ? null : TravelDocumentType.Passport,
            DocumentNumber: leadDocumentNumber,
            DocumentIssuingCountry: leadDocumentNumber is null ? null : "BG",
            DocumentExpiryDate: leadDocumentNumber is null ? null : new DateOnly(2031, 1, 1),
            IsLead: true, Email: "ivan.petrov@example.com", Phone: "+359888123456"),
        new PassengerRequest("Maria", "Petrova", new DateOnly(1988, 7, 12),
            Gender: Gender.Female, Nationality: "BG"),
    ];

    public static async Task<(ProgramDetailDto Program, DepartureDto Departure)> FirstDepartureAsync(HttpClient agency)
    {
        var programs = await agency.GetFromJsonAsync<List<ProgramDto>>("/api/v1/catalog/programs", Json.Options);
        Assert.NotNull(programs);
        Assert.NotEmpty(programs);

        var detail = await agency.GetFromJsonAsync<ProgramDetailDto>(
            $"/api/v1/catalog/programs/{programs[0].Id}", Json.Options);
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Departures);
        return (detail, detail.Departures[0]);
    }

    public static async Task<SearchResponse> SearchAsync(HttpClient agency, int adults = 2, int children = 0)
    {
        var (program, departure) = await FirstDepartureAsync(agency);
        var response = await agency.PostAsJsonAsync("/api/v1/search",
            new SearchRequest(program.Id, departure.Id, adults, children), Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.OK);

        var search = await response.Content.ReadFromJsonAsync<SearchResponse>(Json.Options);
        Assert.NotNull(search);
        Assert.NotEmpty(search.Offers);
        return search;
    }

    public static async Task<BookingDto> CreateDraftAsync(HttpClient agency, PassengerRequest[]? passengers = null)
    {
        var search = await SearchAsync(agency);
        var offer = search.Offers[0];
        var response = await agency.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
                search.ProgramId, search.DepartureId, offer.HotelId, offer.OfferToken,
                [.. passengers ?? TwoAdults()]),
            Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.Created);

        var booking = await response.Content.ReadFromJsonAsync<BookingDto>(Json.Options);
        Assert.NotNull(booking);
        return booking;
    }
}
