using Odisea.SharedKernel;

namespace Odisea.Modules.Agencies.PublicApi;

/// Paid capabilities an agency's plan unlocks. Modules gate their features on these, never on plan names.
public enum AgencyFeature
{
    /// Record and track reservations sold outside Odisea (free-form payload, public tracking).
    ExternalReservations,
    /// Website inquiry forms with a public submission endpoint.
    InquiryForms,
    /// Outbound e-mail on the agency's behalf (inquiry notifications, auto-replies).
    EmailNotifications,
}

public sealed record AgencyEntitlements(Guid AgencyId, string Plan, bool IsActive, IReadOnlyList<AgencyFeature> Features)
{
    public bool Has(AgencyFeature feature) => Features.Contains(feature);
}

public interface IAgencyEntitlements
{
    Task<AgencyEntitlements?> GetAsync(Guid agencyId, CancellationToken ct);

    /// Throws FeatureNotInPlanException when the plan lacks the feature and AgencySuspendedException
    /// when the agency is suspended; unknown agencies count as having no plan.
    Task RequireAsync(Guid agencyId, AgencyFeature feature, CancellationToken ct);
}

public sealed class FeatureNotInPlanException(AgencyFeature feature, string plan)
    : DomainException($"The feature '{feature}' is not included in the agency's plan ({plan}).")
{
    public AgencyFeature Feature { get; } = feature;
    public string Plan { get; } = plan;
}

public sealed class AgencySuspendedException() : DomainException("The agency is suspended.");

/// Stable ProblemDetails "type" URIs so clients can react to a class of error, not to wording.
public static class ProblemTypes
{
    public const string FeatureNotInPlan = "https://odisea.network/problems/feature-not-in-plan";
    public const string AgencySuspended = "https://odisea.network/problems/agency-suspended";
}
