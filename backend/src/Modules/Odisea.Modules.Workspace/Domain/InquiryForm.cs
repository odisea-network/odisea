using System.Security.Cryptography;
using Odisea.SharedKernel;

namespace Odisea.Modules.Workspace.Domain;

/// A contact / inquiry form the agency embeds on its own website. The public access key is the
/// only thing the website knows; everything else (where to notify, what to auto-reply, which
/// origins may post) is configured here.
public class InquiryForm : Entity
{
    public const int MaxNotifyEmails = 5;
    public const int MaxAllowedOrigins = 10;

    public required Guid AgencyId { get; init; }
    public required string Name { get; set; }
    public required string AccessKey { get; set; }

    /// Normalised "scheme://host[:port]" values; empty means any origin may post.
    public string[] AllowedOrigins { get; set; } = [];

    public string[] NotifyEmails { get; set; } = [];
    public bool AutoReplyEnabled { get; set; }
    public string? AutoReplySubject { get; set; }
    public string? AutoReplyBody { get; set; }

    /// Which submitted fields carry the customer's e-mail and name (form markup is the agency's).
    public string EmailField { get; set; } = "email";
    public string NameField { get; set; } = "name";

    /// A hidden field real visitors leave empty; a filled one is a bot and is dropped silently.
    public string HoneypotField { get; set; } = "botcheck";

    public bool IsEnabled { get; set; } = true;
    public int SubmissionCount { get; set; }
    public int SpamCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class InquirySubmission : Entity
{
    public const int MaxFields = 50;
    public const int MaxKeyLength = 60;
    public const int MaxValueLength = 4000;
    public const int MaxTotalBytes = 32 * 1024;

    public required Guid FormId { get; init; }
    public required Guid AgencyId { get; init; }

    /// JSON object of the submitted fields, verbatim after limits (jsonb).
    public required string Fields { get; init; }

    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }

    public InquiryStatus Status { get; private set; } = InquiryStatus.New;
    public string? Origin { get; init; }
    public string? UserAgent { get; init; }

    /// SHA-256 of the client address: enough to spot a flood, not an address on file.
    public string? IpHash { get; init; }

    public Guid? HandledByUserId { get; private set; }
    public DateTimeOffset? HandledAt { get; private set; }
    public string? InternalNote { get; private set; }

    public void SetStatus(InquiryStatus status, string? note, Guid userId, DateTimeOffset now)
    {
        Status = status;
        if (status != InquiryStatus.New && HandledAt is null)
        {
            HandledByUserId = userId;
            HandledAt = now;
        }
        if (!string.IsNullOrWhiteSpace(note))
            InternalNote = note.Trim();
    }
}

public enum InquiryStatus
{
    New,
    Read,
    Replied,
    Archived,
    Spam,
}

public static class FormAccessKey
{
    public const string Prefix = "odf_";
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    private const int RandomLength = 24;

    public static string New() => Prefix + new string(RandomNumberGenerator.GetItems<char>(Alphabet, RandomLength));

    public static bool LooksValid(string? key) =>
        key is { Length: RandomLength + 4 } && key.StartsWith(Prefix, StringComparison.Ordinal)
        && key[Prefix.Length..].All(Alphabet.Contains);
}

public static class WebOrigin
{
    /// "https://www.example.com:8443/path?x" → "https://www.example.com:8443"; null when not an absolute http(s) URL.
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
            return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return null;
        return $"{uri.Scheme}://{uri.Authority}".ToLowerInvariant();
    }
}

public sealed class OriginNotAllowedException() : DomainException("This form does not accept submissions from that website.");

public sealed class FormInUseException(int submissions)
    : DomainException($"The form has {submissions} submission(s); disable it instead of deleting.");
