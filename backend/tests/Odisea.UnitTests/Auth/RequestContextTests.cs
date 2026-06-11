using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Odisea.Application.Common.Interfaces;
using Odisea.WebAPI.Services;

namespace Odisea.UnitTests.Auth;

public class RequestContextTests
{
    private static RequestContext WithClaims(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };
        return new RequestContext(new HttpContextAccessor { HttpContext = ctx });
    }

    [Fact]
    public void AgencyId_PlatformAdminWithoutTenantClaim_ReturnsNull()
    {
        // PlatformAdmin tokens carry no tenantId claim; reading AgencyId must not throw.
        var ctx = WithClaims(new Claim(ClaimTypes.Role, "PlatformAdmin"));

        Assert.Null(ctx.AgencyId);
        Assert.False(ctx.HasAgency);
    }

    [Fact]
    public void AgencyId_AgencyUser_ParsesTenantClaim()
    {
        var agencyId = Guid.NewGuid();
        var ctx = WithClaims(
            new Claim("tenantType", "Agency"),
            new Claim("tenantId", agencyId.ToString()));

        Assert.Equal(agencyId, ctx.AgencyId);
        Assert.True(ctx.HasAgency);
    }

    [Fact]
    public void RequireAgency_NoAgency_Throws()
    {
        IAgencyContext ctx = WithClaims(new Claim(ClaimTypes.Role, "PlatformAdmin"));

        Assert.Throws<InvalidOperationException>(() => ctx.RequireAgency());
    }

    [Fact]
    public void RequireAgency_AgencyUser_ReturnsId()
    {
        var agencyId = Guid.NewGuid();
        IAgencyContext ctx = WithClaims(new Claim("tenantId", agencyId.ToString()));

        Assert.Equal(agencyId, ctx.RequireAgency());
    }

    [Fact]
    public void HasAgency_TenantTypeAgencyButNoTenantId_IsFalse()
    {
        // Regression for #43: a token with tenantType=Agency but no tenantId (e.g.
        // a misseeded PlatformAdmin) must NOT sneak past HasAgency — that gate fed
        // straight into RequireAgency() and produced a 500 in ApiKeysController.
        var ctx = WithClaims(new Claim("tenantType", "Agency"));

        Assert.Null(ctx.AgencyId);
        Assert.False(ctx.HasAgency);
    }

    [Fact]
    public void HasAgency_TenantTypeAgencyAndValidTenantId_IsTrue()
    {
        // Sanity sentinel for the happy path: both claims present, both parseable.
        var ctx = WithClaims(
            new Claim("tenantType", "Agency"),
            new Claim("tenantId", Guid.NewGuid().ToString()));

        Assert.True(ctx.HasAgency);
    }
}
