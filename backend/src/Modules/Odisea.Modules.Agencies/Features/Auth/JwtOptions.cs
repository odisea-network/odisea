namespace Odisea.Modules.Agencies.Features.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "odisea";
    public string Audience { get; set; } = "odisea";

    /// Short by design — the refresh cookie carries the session.
    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;
}
