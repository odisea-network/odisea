using Odisea.Domain.Entities;

namespace Odisea.Application.Suppliers.Dtos;

public record EntitlementDto(
    Guid Id,
    Guid OperatorId,
    Guid AgencyId,
    string Status,
    decimal CommissionPercent,
    DateTime CreatedAt
);

public record CreateEntitlementRequest(
    Guid OperatorId,
    Guid AgencyId,
    decimal CommissionPercent
);

public static class EntitlementMappings
{
    public static EntitlementDto ToDto(this OperatorAgencyEntitlement e) => new(
        e.Id, e.OperatorId, e.AgencyId, e.Status.ToString(),
        e.CommissionPercent, e.CreatedAt);
}
