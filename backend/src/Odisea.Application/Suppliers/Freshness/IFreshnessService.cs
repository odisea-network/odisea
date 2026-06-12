namespace Odisea.Application.Suppliers.Freshness;

// Soft-expire: marks source offers (and the offers normalized from them) Stale
// when they haven't been seen within their connection's freshness TTL. Never
// deletes — stale offers stay queryable but can be filtered out of live
// publications. Returns how many rows it touched so the caller can report it.
public interface IFreshnessService
{
    Task<FreshnessSweepResult> SweepAsync(Guid supplierConnectionId, CancellationToken ct);
}

public sealed record FreshnessSweepResult(int SourceOffersMarkedStale, int OffersMarkedStale);
