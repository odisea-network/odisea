using System.Net;
using System.Net.Http.Json;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Catalog.Features.Programs;
using Odisea.Modules.Catalog.Features.Search;

namespace Odisea.IntegrationTests;

/// The agency-side happy path over HTTP, reusable by every test that needs a booking.
public static class AgencyFlow
{
    public static async Task<(ProgramDetailDto Program, DepartureDto Departure)> FirstDepartureAsync(HttpClient agency)
    {
        var programs = await agency.GetFromJsonAsync<List<ProgramDto>>("/api/v1/catalog/programs");
        Assert.NotNull(programs);
        Assert.NotEmpty(programs);

        var detail = await agency.GetFromJsonAsync<ProgramDetailDto>($"/api/v1/catalog/programs/{programs[0].Id}");
        Assert.NotNull(detail);
        Assert.NotEmpty(detail.Departures);
        return (detail, detail.Departures[0]);
    }

    public static async Task<SearchResponse> SearchAsync(HttpClient agency, int adults = 2, int children = 0)
    {
        var (program, departure) = await FirstDepartureAsync(agency);
        var response = await agency.PostAsJsonAsync("/api/v1/search",
            new SearchRequest(program.Id, departure.Id, adults, children));
        await response.ShouldBeAsync(HttpStatusCode.OK);

        var search = await response.Content.ReadFromJsonAsync<SearchResponse>();
        Assert.NotNull(search);
        Assert.NotEmpty(search.Offers);
        return search;
    }

    public static async Task<BookingDto> CreateDraftAsync(HttpClient agency)
    {
        var search = await SearchAsync(agency);
        var offer = search.Offers[0];
        var response = await agency.PostAsJsonAsync("/api/v1/bookings", new CreateBookingRequest(
            search.ProgramId, search.DepartureId, offer.HotelId, offer.OfferToken,
            [
                new PassengerRequest("Ivan", "Petrov", new DateOnly(1985, 5, 1), IsLead: true),
                new PassengerRequest("Maria", "Petrova", new DateOnly(1988, 7, 12)),
            ]));
        await response.ShouldBeAsync(HttpStatusCode.Created);

        var booking = await response.Content.ReadFromJsonAsync<BookingDto>();
        Assert.NotNull(booking);
        return booking;
    }
}
