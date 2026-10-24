using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Branding;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class BrandingServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 24, 15, 0, 0, TimeSpan.Zero);
    }

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R'];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];
    private static readonly byte[] WebpSignature = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, (byte)'V', (byte)'P', (byte)'8'];

    private readonly AgenciesDbContext _db;
    private readonly FakeClock _clock = new();
    private readonly BrandingService _service;

    public BrandingServiceTests()
    {
        _db = new AgenciesDbContext(new DbContextOptionsBuilder<AgenciesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _service = new BrandingService(_db, _clock);
    }

    [Fact]
    public async Task Defaults_apply_until_the_operator_brands_the_deployment()
    {
        var branding = await _service.GetAsync(CancellationToken.None);

        Assert.Equal("Odisea", branding.DisplayName);
        Assert.Equal("#1f5eff", branding.PrimaryColor);
        Assert.False(branding.HasLogo);
        Assert.Null(branding.LogoUrl);
        Assert.Null(branding.UpdatedAt);
        Assert.Equal(0, await _db.OperatorBranding.CountAsync());
    }

    [Fact]
    public async Task Update_creates_the_single_row_and_then_edits_it_in_place()
    {
        await _service.UpdateAsync(new UpdateBrandingRequest("Paradise Travel ", "#0A7C86", "#F2A900",
            " Office@Paradise.Example ", "+359 2 123 456", "  Paradise Travel Ltd, Sofia  "), CancellationToken.None);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        var second = await _service.UpdateAsync(new UpdateBrandingRequest("Paradise Travel", "#0a7c86", "#f2a900"),
            CancellationToken.None);

        Assert.Equal(1, await _db.OperatorBranding.CountAsync());
        var row = await _db.OperatorBranding.SingleAsync();
        Assert.Equal(OperatorBranding.SingletonId, row.Id);
        Assert.Equal("Paradise Travel", second.DisplayName);
        Assert.Equal("#0a7c86", second.PrimaryColor);
        Assert.Null(second.SupportEmail);
        Assert.Equal(_clock.UtcNow, second.UpdatedAt);
    }

    [Fact]
    public async Task Update_normalises_colours_contact_and_whitespace()
    {
        var branding = await _service.UpdateAsync(new UpdateBrandingRequest(" Paradise ", "#0A7C86", "#F2A900",
            " Office@Paradise.Example ", " +359 2 123 456 ", "   "), CancellationToken.None);

        Assert.Equal("Paradise", branding.DisplayName);
        Assert.Equal("#0a7c86", branding.PrimaryColor);
        Assert.Equal("#f2a900", branding.AccentColor);
        Assert.Equal("office@paradise.example", branding.SupportEmail);
        Assert.Equal("+359 2 123 456", branding.SupportPhone);
        Assert.Null(branding.LegalFooter);
    }

    [Theory]
    [InlineData("png", "image/png")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public async Task Logo_type_is_detected_from_the_bytes(string kind, string expectedType)
    {
        var bytes = kind switch { "png" => PngSignature, "jpeg" => JpegSignature, _ => WebpSignature };

        var branding = await _service.SetLogoAsync(bytes, null, CancellationToken.None);
        var logo = await _service.GetLogoAsync(CancellationToken.None);

        Assert.True(branding.HasLogo);
        Assert.Equal(BrandingDtoMapping.LogoPath, branding.LogoUrl);
        Assert.Equal(12, branding.LogoVersion!.Length);
        Assert.NotNull(logo);
        Assert.Equal(expectedType, logo.ContentType);
        Assert.Equal(bytes, logo.Bytes);
        Assert.StartsWith(branding.LogoVersion, logo.Hash);
    }

    [Fact]
    public async Task Svg_unknown_mismatched_empty_and_oversized_logos_are_rejected()
    {
        await Assert.ThrowsAsync<BrandingValidationException>(() => _service.SetLogoAsync(
            "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"u8.ToArray(), "image/svg+xml", CancellationToken.None));
        await Assert.ThrowsAsync<BrandingValidationException>(() => _service.SetLogoAsync(
            [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], null, CancellationToken.None));
        await Assert.ThrowsAsync<BrandingValidationException>(() => _service.SetLogoAsync(
            PngSignature, "image/jpeg", CancellationToken.None));
        await Assert.ThrowsAsync<BrandingValidationException>(() => _service.SetLogoAsync(
            [], "image/png", CancellationToken.None));

        var tooBig = new byte[BrandingService.MaxLogoBytes + 1];
        PngSignature.CopyTo(tooBig, 0);
        await Assert.ThrowsAsync<BrandingValidationException>(() => _service.SetLogoAsync(
            tooBig, "image/png", CancellationToken.None));

        Assert.Null(await _service.GetLogoAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Declared_type_with_parameters_is_accepted_when_it_matches()
    {
        var branding = await _service.SetLogoAsync(PngSignature, "image/png; charset=binary", CancellationToken.None);
        Assert.True(branding.HasLogo);
    }

    [Fact]
    public async Task Removing_the_logo_keeps_the_rest_of_the_branding()
    {
        await _service.UpdateAsync(new UpdateBrandingRequest("Paradise", "#0a7c86", "#f2a900"), CancellationToken.None);
        await _service.SetLogoAsync(PngSignature, null, CancellationToken.None);

        var branding = await _service.RemoveLogoAsync(CancellationToken.None);

        Assert.False(branding.HasLogo);
        Assert.Null(branding.LogoUrl);
        Assert.Equal("Paradise", branding.DisplayName);
        Assert.Null(await _service.GetLogoAsync(CancellationToken.None));
    }
}
