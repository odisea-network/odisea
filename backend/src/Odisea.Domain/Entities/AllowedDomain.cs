using Odisea.Domain.Common;

namespace Odisea.Domain.Entities;

/// <summary>
/// An origin permitted to embed a <see cref="Publication"/>. A Publication with
/// no AllowedDomain rows is open to any origin (with a logged warning); one or
/// more rows restrict embedding to matching origins. Exact host match, or a
/// <c>*.example.com</c> wildcard that also matches the apex.
/// </summary>
public class AllowedDomain : Entity
{
    public Guid PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;

    public string Domain { get; set; } = string.Empty;

    /// <summary>True if <paramref name="host"/> is permitted by this entry.</summary>
    public bool Matches(string host)
    {
        if (string.IsNullOrEmpty(host)) return false;

        if (Domain.StartsWith("*.", StringComparison.Ordinal))
        {
            var apex = Domain[2..];
            return host.Equals(apex, StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith("." + apex, StringComparison.OrdinalIgnoreCase);
        }

        return host.Equals(Domain, StringComparison.OrdinalIgnoreCase);
    }
}
