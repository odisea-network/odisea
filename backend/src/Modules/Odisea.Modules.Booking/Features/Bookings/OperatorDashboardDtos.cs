using System.ComponentModel.DataAnnotations;

namespace Odisea.Modules.Booking.Features.Bookings;

// Operator-only shapes (the "Operator" prefix keeps them out of the agency net-cost leak guard).
// Every figure is computed here so the portal only ever displays money.

public record OperatorDashboardQuery
{
    /// Window for the created / confirmed figures and the top agencies.
    [Range(1, 90)]
    public int Days { get; init; } = 7;
}

public record OperatorDashboardKpi(int Count, decimal Amount, string Currency);

/// One reservation that needs an operator, most urgent kinds first.
public record OperatorAttentionItem(
    Guid Id,
    string Ref,
    Guid AgencyId,
    /// overdue | failed | ready | soon | partial
    string Kind,
    string Text,
    decimal Amount,
    string Currency,
    DateTimeOffset? At);

public record OperatorAgencyFigure(Guid AgencyId, int Bookings, decimal Sell, string Currency);

public record OperatorDashboardDto(
    DateTimeOffset AsOf,
    int Days,
    OperatorDashboardKpi ReadyToConfirm,
    OperatorDashboardKpi DueSoon,
    OperatorDashboardKpi Overdue,
    OperatorDashboardKpi FailedHolds,
    IReadOnlyList<OperatorAttentionItem> Queue,
    int Created,
    int CreatedPrevious,
    int CreatedViaApi,
    decimal ConfirmedSell,
    decimal ConfirmedMargin,
    string Currency,
    IReadOnlyList<OperatorAgencyFigure> TopAgencies);
