namespace Odisea.Modules.Agencies.PublicApi;

/// Names the host needs for Swagger and rate limiting; the handler itself stays in the module.
public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    /// Every issued key starts with this, so a leaked key is recognisable as ours in scanners.
    public const string KeyPrefixMarker = "odk_live_";

    /// The default scheme: a policy scheme that forwards to ApiKey when the header is
    /// present and to JWT bearer otherwise. Header wins.
    public const string SelectorScheme = "JwtOrApiKey";
}
