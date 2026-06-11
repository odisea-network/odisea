using Microsoft.EntityFrameworkCore;
using Odisea.Application.Analytics.Dtos;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.Application.Analytics;

public static class EventIngestor
{
    /// <summary>
    /// Persists a batch of events. Events whose PublicationKey does not exist are
    /// dropped silently (no error) so the endpoint never leaks which keys are valid.
    /// IP / user-agent are pre-hashed by the caller — raw values never reach here.
    /// Returns the number of events actually persisted.
    /// </summary>
    public static async Task<int> IngestAsync(
        IngestEventCommand command,
        string? ipHash,
        string? userAgentHash,
        IAppDbContext db,
        CancellationToken ct = default)
    {
        if (command.Events.Count == 0) return 0;

        var requestedKeys = command.Events
            .Select(e => e.PublicationKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct()
            .ToList();

        if (requestedKeys.Count == 0) return 0;

        var knownKeys = await db.Publications
            .Where(p => requestedKeys.Contains(p.Key))
            .Select(p => p.Key)
            .ToListAsync(ct);

        var knownSet = knownKeys.ToHashSet();

        var entities = new List<Event>();
        foreach (var dto in command.Events)
        {
            if (!knownSet.Contains(dto.PublicationKey)) continue;
            if (!Enum.TryParse<EventType>(dto.EventType, ignoreCase: true, out var type)) continue;

            var channel = Enum.TryParse<Channel>(dto.Channel, ignoreCase: true, out var ch)
                ? ch
                : Channel.WebComponent;

            entities.Add(new Event
            {
                EventType = type,
                PublicationKey = dto.PublicationKey,
                OfferId = dto.OfferId,
                Channel = channel,
                OccurredAt = dto.OccurredAt?.ToUniversalTime() ?? DateTime.UtcNow,
                IpHash = ipHash,
                UserAgentHash = userAgentHash,
            });
        }

        if (entities.Count == 0) return 0;

        db.Events.AddRange(entities);
        await db.SaveChangesAsync(ct);
        return entities.Count;
    }
}
