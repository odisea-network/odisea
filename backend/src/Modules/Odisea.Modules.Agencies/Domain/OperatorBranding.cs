using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.Domain;

/// The operator's public face for the white-label portal: one row per deployment (each tour
/// operator runs its own Odisea instance). The portal reads it before anyone logs in, so it
/// must never contain anything confidential.
public class OperatorBranding : Entity
{
    /// Fixed id: the primary key keeps the table at one row even under concurrent first writes.
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public required string DisplayName { get; set; }

    /// #rrggbb, lower case.
    public required string PrimaryColor { get; set; }

    public required string AccentColor { get; set; }
    public string? SupportEmail { get; set; }
    public string? SupportPhone { get; set; }
    public string? LegalFooter { get; set; }

    /// Raster only (PNG, JPEG, WebP). SVG is deliberately excluded: it can carry scripts.
    public byte[]? Logo { get; set; }

    public string? LogoContentType { get; set; }

    /// SHA-256 of the logo bytes, hex. Doubles as the ETag and the portal's cache-busting version.
    public string? LogoHash { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    public bool HasLogo => Logo is { Length: > 0 } && LogoContentType is not null && LogoHash is not null;
}

public sealed class BrandingValidationException(string message) : DomainException(message);
