using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Odisea.Modules.Agencies.Domain;
using Odisea.Modules.Agencies.Features.Auth;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Agencies;

public class TokenServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
    }

    private readonly TokenService _service = new(
        Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-at-least-32-bytes-long!",
            Issuer = "odisea-test",
            Audience = "odisea-test",
            AccessTokenMinutes = 30,
        }),
        new FakeClock());

    [Fact]
    public void Agency_user_token_contains_scoping_claims()
    {
        var agencyId = Guid.NewGuid();
        var user = new AgencyUser
        {
            AgencyId = agencyId,
            Email = "agent@test.local",
            PasswordHash = "x",
            FullName = "Test Agent",
            Role = AgencyUserRole.AgencyAdmin,
        };

        var (token, expiresAt) = _service.IssueFor(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("odisea-test", parsed.Issuer);
        Assert.Equal(user.Id.ToString(), parsed.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("agency", parsed.Claims.Single(c => c.Type == "user_type").Value);
        Assert.Equal("AgencyAdmin", parsed.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal(agencyId.ToString(), parsed.Claims.Single(c => c.Type == "agency_id").Value);
        Assert.Equal(expiresAt.UtcDateTime, parsed.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Operator_token_has_no_agency_id()
    {
        var user = new OperatorUser
        {
            Email = "admin@test.local",
            PasswordHash = "x",
            FullName = "Test Admin",
            Role = OperatorRole.Admin,
        };

        var (token, _) = _service.IssueFor(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("operator", parsed.Claims.Single(c => c.Type == "user_type").Value);
        Assert.DoesNotContain(parsed.Claims, c => c.Type == "agency_id");
    }
}
