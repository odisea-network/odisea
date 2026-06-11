using Odisea.Domain.Entities;

namespace Odisea.Application.Suppliers.Dtos;

public record SupplierConnectionDto(
    Guid Id,
    Guid OperatorId,
    string Kind,
    string Name,
    string Status,
    DateTime? LastSyncedAt,
    DateTime CreatedAt
);

public static class SupplierConnectionMappings
{
    public static SupplierConnectionDto ToDto(this SupplierConnection c) => new(
        c.Id, c.OperatorId, c.Kind.ToString(), c.Name,
        c.Status.ToString(), c.LastSyncedAt, c.CreatedAt);
}
