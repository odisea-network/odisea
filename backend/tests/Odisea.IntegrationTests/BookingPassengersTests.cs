using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Modules.Booking.Features.Bookings;
using Odisea.Modules.Booking.Infrastructure;

namespace Odisea.IntegrationTests;

[Collection(ApiCollection.Name)]
public class BookingPassengersTests(OdiseaApiFactory factory)
{
    [Fact]
    public async Task Document_numbers_are_ciphertext_in_the_database_and_plaintext_in_the_api()
    {
        var agency = factory.ClientAs(TestIdentity.Agent);
        var booking = await AgencyFlow.CreateDraftAsync(agency, AgencyFlow.TwoAdults(leadDocumentNumber: "AB1234567"));

        var fetched = await agency.GetFromJsonAsync<BookingDto>($"/api/v1/bookings/{booking.Id}", Json.Options);
        Assert.NotNull(fetched);
        var lead = fetched.Passengers.Single(p => p.IsLead);
        Assert.Equal("AB1234567", lead.DocumentNumber);
        Assert.Equal("Passport", lead.DocumentType);
        Assert.Equal("Male", lead.Gender);
        Assert.Equal("Adult", lead.PassengerType);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var stored = await db.Database
            .SqlQuery<string>($"""
                SELECT document_number AS "Value"
                FROM booking.booking_passengers
                WHERE booking_id = {booking.Id} AND is_lead
                """)
            .ToListAsync();

        var raw = Assert.Single(stored);
        Assert.DoesNotContain("AB1234567", raw);
        Assert.True(raw.Length > 40, "ciphertext should be far longer than the 9-character document number");
    }

    [Fact]
    public async Task Passengers_can_be_replaced_while_the_booking_is_a_draft()
    {
        var agency = factory.ClientAs(TestIdentity.Agent);
        var booking = await AgencyFlow.CreateDraftAsync(agency);

        var response = await agency.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/passengers",
            new UpdatePassengersRequest([
                new PassengerRequest("Georgi", "Dimitrov", new DateOnly(1985, 3, 3), IsLead: true),
                new PassengerRequest("Elena", "Dimitrova", new DateOnly(1986, 4, 4)),
            ]),
            Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.OK);

        var updated = await response.Content.ReadFromJsonAsync<BookingDto>(Json.Options);
        Assert.NotNull(updated);
        Assert.Equal(["Georgi", "Elena"], updated.Passengers.Select(p => p.FirstName).ToArray());
    }

    [Fact]
    public async Task Unknown_booking_is_a_404()
    {
        var agency = factory.ClientAs(TestIdentity.Agent);
        var response = await agency.PutAsJsonAsync($"/api/v1/bookings/{Guid.NewGuid()}/passengers",
            new UpdatePassengersRequest([new PassengerRequest("A", "B")]), Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_passenger_data_is_a_400_from_model_validation()
    {
        var agency = factory.ClientAs(TestIdentity.Agent);
        var booking = await AgencyFlow.CreateDraftAsync(agency);

        var response = await agency.PutAsJsonAsync($"/api/v1/bookings/{booking.Id}/passengers",
            new UpdatePassengersRequest([
                new PassengerRequest("Иван", "Петров", new DateOnly(1985, 3, 3), Nationality: "Bulgaria"),
            ]),
            Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }
}
