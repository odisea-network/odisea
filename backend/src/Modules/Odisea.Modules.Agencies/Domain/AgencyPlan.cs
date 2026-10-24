using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Domain;

/// Commercial tier. Core is what every partner gets (search and book the operator's packages);
/// Workspace is the first paid add-on: the agency's own back-office tools inside Odisea.
public enum AgencyPlan
{
    Core,
    Workspace,
}

public static class PlanFeatures
{
    private static readonly AgencyFeature[] None = [];

    private static readonly AgencyFeature[] Workspace =
    [
        AgencyFeature.ExternalReservations,
        AgencyFeature.InquiryForms,
        AgencyFeature.EmailNotifications,
    ];

    public static IReadOnlyList<AgencyFeature> For(AgencyPlan plan) => plan switch
    {
        AgencyPlan.Workspace => Workspace,
        _ => None,
    };
}
