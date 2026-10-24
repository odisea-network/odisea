using System.ComponentModel.DataAnnotations;
using Odisea.Modules.Agencies.Domain;

namespace Odisea.Modules.Agencies.Features.Branding;

public static class BrandingPatterns
{
    public const string HexColor = "^#[0-9a-fA-F]{6}$";
    public const string Phone = @"^\+?[0-9 ()\-]{6,30}$";
}

/// What the portal needs to dress itself. Served anonymously, so nothing confidential belongs here.
public record BrandingDto(
    string DisplayName,
    string PrimaryColor,
    string AccentColor,
    string? SupportEmail,
    string? SupportPhone,
    string? LegalFooter,
    bool HasLogo,
    string? LogoUrl,
    string? LogoVersion,
    DateTimeOffset? UpdatedAt);

public record UpdateBrandingRequest(
    [Required, MaxLength(100)] string DisplayName,
    [Required, RegularExpression(BrandingPatterns.HexColor)] string PrimaryColor,
    [Required, RegularExpression(BrandingPatterns.HexColor)] string AccentColor,
    [EmailAddress, MaxLength(320)] string? SupportEmail = null,
    [RegularExpression(BrandingPatterns.Phone)] string? SupportPhone = null,
    [MaxLength(500)] string? LegalFooter = null);

public static class BrandingDtoMapping
{
    public const string LogoPath = "/api/v1/branding/logo";

    public static BrandingDto ToDto(this OperatorBranding b) => new(
        b.DisplayName, b.PrimaryColor, b.AccentColor,
        b.SupportEmail, b.SupportPhone, b.LegalFooter,
        b.HasLogo,
        b.HasLogo ? LogoPath : null,
        b.HasLogo ? b.LogoHash![..12] : null,
        b.UpdatedAt);
}
