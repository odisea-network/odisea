using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Odisea.Api;
using Odisea.Modules.Catalog.Domain;
using Odisea.Modules.Catalog.Features.Destinations;
using Odisea.Modules.Catalog.Features.Hotels;
using Odisea.Modules.Catalog.Features.Programs;

namespace Odisea.IntegrationTests;

/// A program from brief to published over HTTP: content in one call, gallery upload, the public
/// image endpoint, readiness gating publish, and what the agency catalogue shows afterwards.
[Collection("api")]
public class ProgramContentTests(OdiseaApiFactory factory)
{
    private static SaveProgramContentRequest Content(string? slug = null) => new(
        Tagline: "7 nights all-inclusive with a direct charter",
        Summary: "A week of early-season sun with a direct charter, transfers and insurance all in one price.",
        Highlights: ["Direct charter", "Three hotels", "Transfers included"],
        Inclusions: ["Return charter flight", "7 nights all-inclusive", "Transfers"],
        Exclusions: ["Optional excursions"],
        Tags: ["all-inclusive", "family"],
        TransportMode: TransportMode.Flight,
        TransportDetails: "Charter SOF-AYT, 20 kg baggage",
        DeparturePoint: "Sofia Airport (SOF)",
        MinGroupSize: null,
        MaxGroupSize: null,
        PracticalInfo: "Passport valid 6 months after return",
        CancellationPolicy: "Free until 30 days before departure",
        PaymentTerms: "30% deposit, balance 21 days before departure",
        InsurancePolicy: "Medical insurance included",
        ChildPolicy: "Children 2–11 at the child rate",
        Itinerary:
        [
            new ItineraryDayRequest(1, "Arrival", "Transfer and check-in", MealPlan.Dinner, "Antalya"),
            new ItineraryDayRequest(2, "Beach day", null, MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, "Antalya"),
            new ItineraryDayRequest(3, "Return", "Check-out and flight home", MealPlan.Breakfast, null),
        ],
        Slug: slug);

    private static MultipartFormDataContent ImageForm(byte[] bytes, string name, string? altText, bool isCover)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var form = new MultipartFormDataContent { { file, "file", name }, { new StringContent(isCover ? "true" : "false"), "isCover" } };
        if (altText is not null)
            form.Add(new StringContent(altText), "altText");
        return form;
    }

    private static async Task<ProgramDetailDto> ReadAsync(HttpResponseMessage response)
    {
        var detail = await response.Content.ReadFromJsonAsync<ProgramDetailDto>(Json.Options);
        Assert.NotNull(detail);
        return detail;
    }

    [Fact]
    public async Task Agents_cannot_touch_program_content_or_images()
    {
        using var agent = factory.ClientAs(TestIdentity.Agent);
        var id = Guid.NewGuid();

        var content = await agent.PutAsJsonAsync($"/api/v1/programs/{id}/content", Content(), Json.Options);
        await content.ShouldBeAsync(HttpStatusCode.Forbidden);
        var upload = await agent.PostAsync($"/api/v1/programs/{id}/images", ImageForm(DemoImages.Png(4, 4, (0, 0, 0), (9, 9, 9)), "x.png", null, false));
        await upload.ShouldBeAsync(HttpStatusCode.Forbidden);
        var readiness = await agent.GetAsync($"/api/v1/programs/{id}/readiness");
        await readiness.ShouldBeAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Operator_takes_a_program_from_brief_to_the_agency_catalogue()
    {
        using var op = factory.ClientAs(TestIdentity.Operator);
        using var agent = factory.ClientAs(TestIdentity.Agent);
        using var anonymous = factory.AnonymousClient();

        var destinations = await op.GetFromJsonAsync<List<DestinationDto>>("/api/v1/destinations", Json.Options);
        var destination = Assert.Single(destinations!, d => d.Name == "Antalya");
        var hotels = await op.GetFromJsonAsync<List<HotelDto>>($"/api/v1/hotels?destinationId={destination.Id}", Json.Options);
        Assert.NotEmpty(hotels!);

        // One call creates the program with its whole content block.
        var name = $"Bodrum Spring {Guid.NewGuid():N}"[..40];
        var created = await op.PostAsJsonAsync("/api/v1/programs",
            new CreateProgramRequest(name, destination.Id, "mock", "Spring 2027", new string('d', 400), Content()), Json.Options);
        await created.ShouldBeAsync(HttpStatusCode.Created);
        var program = await ReadAsync(created);
        Assert.Equal(3, program.Content.Highlights.Count);
        Assert.Equal(3, program.Itinerary.Count);
        Assert.Equal(MealPlan.Breakfast | MealPlan.Lunch | MealPlan.Dinner, program.Itinerary[1].Meals);
        Assert.StartsWith("bodrum-spring-", program.Slug);
        Assert.NotNull(program.Readiness);
        Assert.False(program.Readiness.CanPublish);
        Assert.Contains(program.Readiness.Checks, c => c is { Code: "cover", Required: true, Passed: false });

        // Publishing is refused with the list of what is missing.
        var refused = await op.PostAsync($"/api/v1/programs/{program.Id}/publish", null);
        await refused.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);
        var problem = await refused.Content.ReadAsStringAsync();
        Assert.Contains("cover image", problem);
        Assert.Contains("upcoming departure", problem);

        var departure = await op.PostAsJsonAsync($"/api/v1/programs/{program.Id}/departures",
            new CreateDepartureRequest(new DateOnly(2027, 5, 1), new DateOnly(2027, 5, 3), "Charter SOF-BJV"), Json.Options);
        await departure.ShouldBeAsync(HttpStatusCode.Created);
        var hotel = await op.PostAsync($"/api/v1/programs/{program.Id}/hotels/{hotels![0].Id}", null);
        await hotel.ShouldBeAsync(HttpStatusCode.NoContent);

        // Gallery: the first upload becomes the cover; the bytes are public and cacheable.
        var cover = DemoImages.Png(1600, 900, (33, 110, 170), (150, 205, 230));
        var uploaded = await op.PostAsync($"/api/v1/programs/{program.Id}/images", ImageForm(cover, "beach.png", "Sandy beach", false));
        await uploaded.ShouldBeAsync(HttpStatusCode.Created);
        var image = await uploaded.Content.ReadFromJsonAsync<ProgramImageDto>(Json.Options);
        Assert.NotNull(image);
        Assert.True(image.IsCover);
        Assert.Equal((1600, 900, "image/png"), (image.Width, image.Height, image.ContentType));

        var bytes = await anonymous.GetAsync(image.Url);
        await bytes.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("image/png", bytes.Content.Headers.ContentType?.MediaType);
        Assert.Equal(cover.Length, (await bytes.Content.ReadAsByteArrayAsync()).Length);
        Assert.Contains("immutable", bytes.Headers.CacheControl?.ToString());
        Assert.NotNull(bytes.Headers.ETag);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, image.Url);
        conditional.Headers.IfNoneMatch.Add(bytes.Headers.ETag);
        var notModified = await anonymous.SendAsync(conditional);
        await notModified.ShouldBeAsync(HttpStatusCode.NotModified);

        var rejected = await op.PostAsync($"/api/v1/programs/{program.Id}/images",
            ImageForm("<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray(), "evil.svg", null, false));
        await rejected.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);

        // Slugs can be chosen but never shared.
        var slugged = await op.PutAsJsonAsync($"/api/v1/programs/{program.Id}/content", Content(slug: program.Slug + "-x"), Json.Options);
        await slugged.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal(program.Slug + "-x", (await ReadAsync(slugged)).Slug);
        var clash = await op.PostAsJsonAsync("/api/v1/programs",
            new CreateProgramRequest("Clash", destination.Id, "mock", null, null, Content(slug: program.Slug + "-x")), Json.Options);
        await clash.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);

        var published = await op.PostAsync($"/api/v1/programs/{program.Id}/publish", null);
        await published.ShouldBeAsync(HttpStatusCode.OK);
        var live = await ReadAsync(published);
        Assert.Equal("Published", live.Status);
        Assert.True(live.Readiness!.CanPublish);

        // The agency sees the content, the gallery and the itinerary — and no readiness.
        var list = await agent.GetFromJsonAsync<List<ProgramDto>>("/api/v1/catalog/programs", Json.Options);
        var card = Assert.Single(list!, p => p.Id == program.Id);
        Assert.Equal(image.Url, card.CoverImageUrl);
        Assert.Equal(["all-inclusive", "family"], card.Tags);
        Assert.NotNull(card.Summary);

        var detail = await agent.GetFromJsonAsync<ProgramDetailDto>($"/api/v1/catalog/programs/{program.Id}", Json.Options);
        Assert.NotNull(detail);
        Assert.Null(detail.Readiness);
        Assert.Single(detail.Images);
        Assert.Equal("Free until 30 days before departure", detail.Content.CancellationPolicy);
        Assert.Equal("Arrival", detail.Itinerary[0].Title);

        // Deleting the image takes the public bytes with it.
        var deleted = await op.DeleteAsync($"/api/v1/programs/{program.Id}/images/{image.Id}");
        await deleted.ShouldBeAsync(HttpStatusCode.NoContent);
        var gone = await anonymous.GetAsync(image.Url);
        await gone.ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Demo_program_ships_with_full_content_and_a_gallery()
    {
        using var agent = factory.ClientAs(TestIdentity.Agent);
        var (program, _) = await AgencyFlow.FirstDepartureAsync(agent);

        Assert.Equal("antalya-easter-2027", program.Slug);
        Assert.Equal(8, program.Itinerary.Count);
        Assert.Equal(3, program.Images.Count);
        Assert.NotEmpty(program.Content.Inclusions);
        Assert.NotNull(program.Content.CancellationPolicy);
    }
}
