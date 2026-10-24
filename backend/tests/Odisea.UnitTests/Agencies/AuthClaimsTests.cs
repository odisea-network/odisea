using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.PublicApi;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class AuthClaimsTests
{
    /// The OperatorAdmin policy compares the role claim (issued from OperatorRole.ToString())
    /// against this constant; renaming the enum member must not silently lock admins out.
    [Fact]
    public void Operator_admin_claim_value_matches_the_role_enum() =>
        Assert.Equal(AuthClaims.RoleOperatorAdmin, OperatorRole.Admin.ToString());
}
