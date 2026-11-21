using Odisea.Modules.Agencies.PublicApi;

namespace Odisea.Modules.Agencies.Domain;

/// Commercial tier. Core is what every partner gets (search and book the operator's packages);
/// Ledger adds the reservation ledger for bookings made with any tour operator;
/// Workspace is the full back-office: everything in Ledger plus the remaining agency tools.
/// Stored by name, so the declaration order carries no meaning for existing rows.
public enum AgencyPlan
{
    Core,
    Ledger,
    Workspace,
}

public static class PlanFeatures
{
    private static readonly AgencyFeature[] None = [];

    private static readonly AgencyFeature[] Ledger = [AgencyFeature.ReservationLedger];

    private static readonly AgencyFeature[] Workspace =
    [
        AgencyFeature.ReservationLedger,
        AgencyFeature.ExternalReservations,
        AgencyFeature.InquiryForms,
        AgencyFeature.EmailNotifications,
    ];

    public static IReadOnlyList<AgencyFeature> For(AgencyPlan plan) => plan switch
    {
        AgencyPlan.Ledger => Ledger,
        AgencyPlan.Workspace => Workspace,
        _ => None,
    };
}
