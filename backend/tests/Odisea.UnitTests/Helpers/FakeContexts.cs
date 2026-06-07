using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.UnitTests.Helpers;

// Simulates a PlatformAdmin (HasAgency = false) when constructed without an agencyId,
// or an agency-scoped user when constructed with one.
public class FakeAgencyContext(Guid? agencyId = null) : IAgencyContext
{
    public bool HasAgency => agencyId.HasValue;
    public Guid AgencyId => agencyId ?? throw new InvalidOperationException("No agency in context");
}

public class FakeUserContext(
    Guid? userId = null,
    string email = "test@test.com",
    UserRole role = UserRole.PlatformAdmin) : IUserContext
{
    public bool IsAuthenticated => true;
    public Guid UserId => userId ?? Guid.NewGuid();
    public string Email => email;
    public UserRole Role => role;
}
