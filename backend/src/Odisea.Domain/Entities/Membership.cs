using Odisea.Domain.Common;
using Odisea.Domain.Enums;

namespace Odisea.Domain.Entities;

// One row per user-per-tenant relationship.
// TenantId is null for PlatformAdmin — they own the whole platform, not a specific tenant.
public class Membership : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public TenantType TenantType { get; set; }
    public Guid? TenantId { get; set; }
    public UserRole Role { get; set; }
}
