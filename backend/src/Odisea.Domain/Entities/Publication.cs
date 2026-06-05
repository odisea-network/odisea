using System.Security.Cryptography;
using Odisea.Domain.Common;
using Odisea.Domain.Enums;
using Odisea.Domain.ValueObjects;

namespace Odisea.Domain.Entities;

public class Publication : Entity
{
    public Guid AgencyId { get; set; }

    /// <summary>
    /// Stable public identifier for embedding — never reused, not a slug.
    /// </summary>
    public string Key { get; set; } = NewKey();

    public Guid CollectionId { get; set; }

    /// <summary>
    /// Nullable until Theme entity lands. Do not add a navigation property yet.
    /// </summary>
    public Guid? ThemeId { get; set; }

    public Guid? ExperienceId { get; set; }

    public ExperienceConfig? ExperienceConfig { get; set; }

    public PublicationStatus Status { get; set; } = PublicationStatus.Draft;

    /// <summary>
    /// Empty = allow all origins. Non-empty = Origin header host must match one entry.
    /// </summary>
    public string[] AllowedDomains { get; set; } = [];

    /// <summary>
    /// Frozen to an integer on Publish. Clients use this for ETag caching.
    /// </summary>
    public int Version { get; set; } = 0;

    private const string KeyAlphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
    private const int KeyLength = 12;

    public static string NewKey()
    {
        Span<byte> bytes = stackalloc byte[KeyLength];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.ToArray().Select(b => KeyAlphabet[b % KeyAlphabet.Length]).ToArray());
    }
}
