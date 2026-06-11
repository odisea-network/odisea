namespace Odisea.Domain.Common;

/// <summary>
/// Canonical API-key scope names. Stored comma-separated on <c>ApiKey.Scopes</c>
/// and emitted as scope claims by the ApiKey authentication scheme.
/// </summary>
public static class ApiKeyScopes
{
    public const string PublicationsRead = "publications:read";
    public const string EventsWrite = "events:write";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { PublicationsRead, EventsWrite };
}
