namespace Odisea.Modules.Agencies.Features.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = "";
    public string Issuer { get; set; } = "odisea";
    public string Audience { get; set; } = "odisea";
    public int AccessTokenMinutes { get; set; } = 60;
}
