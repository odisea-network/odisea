using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

/// <summary>
/// A single analytics signal emitted by an embedded surface (impression, open,
/// inquiry-start, inquiry-submit). Attributed to a publication + channel.
/// </summary>
public class Event : Entity
{
    public EventType EventType { get; set; }

    /// <summary>Stable public embed key the event was attributed to.</summary>
    public string PublicationKey { get; set; } = string.Empty;

    public Guid? OfferId { get; set; }

    /// <summary>Client-supplied event time (UTC). Distinct from <see cref="Entity.CreatedAt"/> insert time.</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Channel Channel { get; set; } = Channel.WebComponent;

    /// <summary>SHA-256 of the user agent. Never the raw value — privacy.</summary>
    public string? UserAgentHash { get; set; }

    /// <summary>SHA-256 of the client IP. Never the raw value — privacy.</summary>
    public string? IpHash { get; set; }
}
