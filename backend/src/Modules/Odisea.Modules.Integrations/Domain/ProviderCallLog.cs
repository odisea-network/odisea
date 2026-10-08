using Odisea.SharedKernel;

namespace Odisea.Modules.Integrations.Domain;

/// One row per outbound provider call. Detail is a short, PII-free summary —
/// never passenger names, tokens, or credentials.
public class ProviderCallLog : Entity
{
    public required string ProviderCode { get; set; }
    public required string Operation { get; set; }
    public long DurationMs { get; set; }
    public bool Success { get; set; }
    public string? Detail { get; set; }
}
