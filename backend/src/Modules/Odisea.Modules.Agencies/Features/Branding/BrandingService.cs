using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Infrastructure;
using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Features.Branding;

public sealed record LogoContent(byte[] Bytes, string ContentType, string Hash);

public class BrandingService(AgenciesDbContext db, IClock clock)
{
    public const int MaxLogoBytes = 512 * 1024;

    /// What a fresh deployment shows until the operator brands it (the portal's own defaults).
    public static readonly BrandingDto Defaults = new(
        "Odisea", "#1f5eff", "#1c2330", null, null, null, false, null, null, null);

    public async Task<BrandingDto> GetAsync(CancellationToken ct) =>
        (await db.OperatorBranding.AsNoTracking().FirstOrDefaultAsync(ct))?.ToDto() ?? Defaults;

    public async Task<BrandingDto> UpdateAsync(UpdateBrandingRequest request, CancellationToken ct)
    {
        var branding = await LoadOrCreateAsync(ct);
        branding.DisplayName = request.DisplayName.Trim();
        branding.PrimaryColor = request.PrimaryColor.ToLowerInvariant();
        branding.AccentColor = request.AccentColor.ToLowerInvariant();
        branding.SupportEmail = Normalize(request.SupportEmail)?.ToLowerInvariant();
        branding.SupportPhone = Normalize(request.SupportPhone);
        branding.LegalFooter = Normalize(request.LegalFooter);
        branding.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return branding.ToDto();
    }

    /// The content type is detected from the bytes, never trusted from the upload; a declared
    /// type that disagrees with the bytes is rejected rather than silently corrected.
    public async Task<BrandingDto> SetLogoAsync(byte[] bytes, string? declaredContentType, CancellationToken ct)
    {
        if (bytes.Length == 0)
            throw new BrandingValidationException("The logo file is empty.");
        if (bytes.Length > MaxLogoBytes)
            throw new BrandingValidationException($"The logo must be at most {MaxLogoBytes / 1024} KB.");

        var detected = DetectImageType(bytes)
            ?? throw new BrandingValidationException("The logo must be a PNG, JPEG or WebP image.");
        if (declaredContentType is { Length: > 0 }
            && !string.Equals(declaredContentType.Split(';')[0].Trim(), detected, StringComparison.OrdinalIgnoreCase))
            throw new BrandingValidationException($"The file content is {detected}, not {declaredContentType}.");

        var branding = await LoadOrCreateAsync(ct);
        branding.Logo = bytes;
        branding.LogoContentType = detected;
        branding.LogoHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        branding.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return branding.ToDto();
    }

    public async Task<LogoContent?> GetLogoAsync(CancellationToken ct)
    {
        var branding = await db.OperatorBranding.AsNoTracking().FirstOrDefaultAsync(ct);
        return branding is { HasLogo: true }
            ? new LogoContent(branding.Logo!, branding.LogoContentType!, branding.LogoHash!)
            : null;
    }

    public async Task<BrandingDto> RemoveLogoAsync(CancellationToken ct)
    {
        var branding = await LoadOrCreateAsync(ct);
        branding.Logo = null;
        branding.LogoContentType = null;
        branding.LogoHash = null;
        branding.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return branding.ToDto();
    }

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    /// Sniffs the magic bytes of the three accepted raster formats.
    public static string? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= PngMagic.Length && bytes[..PngMagic.Length].SequenceEqual(PngMagic))
            return "image/png";
        if (bytes.Length >= JpegMagic.Length && bytes[..JpegMagic.Length].SequenceEqual(JpegMagic))
            return "image/jpeg";
        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes[8..12].SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    private async Task<OperatorBranding> LoadOrCreateAsync(CancellationToken ct)
    {
        var existing = await db.OperatorBranding.FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var created = new OperatorBranding
        {
            Id = OperatorBranding.SingletonId,
            DisplayName = Defaults.DisplayName,
            PrimaryColor = Defaults.PrimaryColor,
            AccentColor = Defaults.AccentColor,
            UpdatedAt = clock.UtcNow,
        };
        db.OperatorBranding.Add(created);
        return created;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
