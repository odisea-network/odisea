using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Odisea.Modules.Agencies.Features.Branding;

namespace Odisea.IntegrationTests;

/// The white-label portal reads branding before anyone logs in; only operator admins change it.
[Collection(ApiCollection.Name)]
public class BrandingTests(OdiseaApiFactory factory)
{
    private static readonly byte[] Png =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 1, 2, 3, 4];

    private static MultipartFormDataContent LogoForm(byte[] bytes, string contentType, string fileName = "logo.png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { content, "file", fileName } };
    }

    [Fact]
    public async Task Branding_is_public_and_cacheable()
    {
        var response = await factory.AnonymousClient().GetAsync("/api/v1/branding");

        await response.ShouldBeAsync(HttpStatusCode.OK);
        Assert.True(response.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromMinutes(5), response.Headers.CacheControl?.MaxAge);
        var branding = await response.Content.ReadFromJsonAsync<BrandingDto>(Json.Options);
        Assert.NotNull(branding);
        Assert.False(string.IsNullOrWhiteSpace(branding.DisplayName));
        Assert.Matches("^#[0-9a-f]{6}$", branding.PrimaryColor);
    }

    [Fact]
    public async Task Only_operator_admins_change_branding_and_everyone_sees_the_result()
    {
        var request = new UpdateBrandingRequest("Paradise Travel", "#0A7C86", "#F2A900", "office@paradise.example", "+359 2 123 456", "Paradise Travel Ltd");

        await (await factory.ClientAs(TestIdentity.Agent).PutAsJsonAsync("/api/v1/branding", request, Json.Options))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await factory.ClientAs(TestIdentity.Operator).PutAsJsonAsync("/api/v1/branding", request, Json.Options))
            .ShouldBeAsync(HttpStatusCode.Forbidden);
        await (await factory.AnonymousClient().PutAsJsonAsync("/api/v1/branding", request, Json.Options))
            .ShouldBeAsync(HttpStatusCode.Unauthorized);

        var updated = await factory.ClientAs(TestIdentity.Admin).PutAsJsonAsync("/api/v1/branding", request, Json.Options);
        await updated.ShouldBeAsync(HttpStatusCode.OK);

        var seen = await factory.AnonymousClient().GetFromJsonAsync<BrandingDto>("/api/v1/branding", Json.Options);
        Assert.NotNull(seen);
        Assert.Equal("Paradise Travel", seen.DisplayName);
        Assert.Equal("#0a7c86", seen.PrimaryColor);
        Assert.Equal("office@paradise.example", seen.SupportEmail);
        Assert.NotNull(seen.UpdatedAt);
    }

    [Fact]
    public async Task Invalid_colours_are_rejected_by_model_validation()
    {
        var response = await factory.ClientAs(TestIdentity.Admin).PutAsJsonAsync("/api/v1/branding",
            new UpdateBrandingRequest("Paradise", "blue", "#f2a900"), Json.Options);
        await response.ShouldBeAsync(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logo_round_trips_with_etag_and_can_be_removed()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);
        var anonymous = factory.AnonymousClient();

        await (await factory.ClientAs(TestIdentity.Operator).PutAsync("/api/v1/branding/logo", LogoForm(Png, "image/png")))
            .ShouldBeAsync(HttpStatusCode.Forbidden);

        var uploaded = await admin.PutAsync("/api/v1/branding/logo", LogoForm(Png, "image/png"));
        await uploaded.ShouldBeAsync(HttpStatusCode.OK);
        var branding = await uploaded.Content.ReadFromJsonAsync<BrandingDto>(Json.Options);
        Assert.NotNull(branding);
        Assert.True(branding.HasLogo);
        Assert.Equal("/api/v1/branding/logo", branding.LogoUrl);
        Assert.NotNull(branding.LogoVersion);

        var logo = await anonymous.GetAsync("/api/v1/branding/logo");
        await logo.ShouldBeAsync(HttpStatusCode.OK);
        Assert.Equal("image/png", logo.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await logo.Content.ReadAsByteArrayAsync());
        Assert.NotNull(logo.Headers.ETag);
        Assert.StartsWith("\"" + branding.LogoVersion, logo.Headers.ETag!.Tag);
        Assert.True(logo.Headers.CacheControl?.Public);

        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/api/v1/branding/logo");
        conditional.Headers.IfNoneMatch.Add(logo.Headers.ETag);
        await (await anonymous.SendAsync(conditional)).ShouldBeAsync(HttpStatusCode.NotModified);

        var removed = await admin.DeleteAsync("/api/v1/branding/logo");
        await removed.ShouldBeAsync(HttpStatusCode.OK);
        Assert.False((await removed.Content.ReadFromJsonAsync<BrandingDto>(Json.Options))!.HasLogo);
        await (await anonymous.GetAsync("/api/v1/branding/logo")).ShouldBeAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logo_type_and_size_are_validated()
    {
        var admin = factory.ClientAs(TestIdentity.Admin);

        var svg = await admin.PutAsync("/api/v1/branding/logo",
            LogoForm("<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray(), "image/svg+xml", "logo.svg"));
        await svg.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);

        var tooBig = new byte[BrandingService.MaxLogoBytes + 1024];
        Png.CopyTo(tooBig, 0);
        var oversized = await admin.PutAsync("/api/v1/branding/logo", LogoForm(tooBig, "image/png"));
        await oversized.ShouldBeAsync(HttpStatusCode.UnprocessableEntity);

        var noFile = await admin.PutAsync("/api/v1/branding/logo", new MultipartFormDataContent());
        await noFile.ShouldBeAsync(HttpStatusCode.BadRequest);
    }
}
