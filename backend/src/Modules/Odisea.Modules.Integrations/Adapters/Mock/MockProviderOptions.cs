namespace Odisea.Modules.Integrations.Adapters.Mock;

public class MockProviderOptions
{
    public const string SectionName = "Providers:Mock";

    /// Percentage [0–100] of re-price/book calls that report no availability.
    public int FailureRatePercent { get; set; }

    /// Percentage [0–100] of re-price calls whose price drifts.
    public int RePriceDriftRatePercent { get; set; }

    /// Drift magnitude in percent applied when a re-price drifts.
    public decimal DriftPercent { get; set; } = 5m;

    /// Artificial latency per call, to make timing realistic in dev.
    public int LatencyMs { get; set; }
}
