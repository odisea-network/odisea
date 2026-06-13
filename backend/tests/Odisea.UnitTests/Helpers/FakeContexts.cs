using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Odisea.Application.Common.Interfaces;
using Odisea.Domain.Enums;

namespace Odisea.UnitTests.Helpers;

// Simulates a PlatformAdmin (HasAgency = false) when constructed without an agencyId,
// or an agency-scoped user when constructed with one.
public class FakeAgencyContext(Guid? agencyId = null) : IAgencyContext
{
    public bool HasAgency => agencyId.HasValue;
    public Guid? AgencyId => agencyId;
}

// Simulates an operator-scoped user when constructed with an operatorId,
// or a non-operator caller (agency / PlatformAdmin) when constructed without one.
public class FakeOperatorContext(Guid? operatorId = null) : IOperatorContext
{
    public bool HasOperator => operatorId.HasValue;
    public Guid? OperatorId => operatorId;
}

// Passthrough policy for tests that don't exercise entitlement gating: returns
// the full offer set. Tests that DO exercise the gate use the real OfferAccessPolicy.
public class FakeOfferAccessPolicy(Odisea.Application.Common.Interfaces.IAppDbContext db)
    : Odisea.Application.Catalog.Access.IOfferAccessPolicy
{
    public Task<IQueryable<Odisea.Domain.Entities.Offer>> DistributableForAgencyAsync(Guid agencyId, CancellationToken ct) =>
        Task.FromResult(db.Offers.AsQueryable());
}

// Records dispatched events so tests can assert on them; never sends anything.
public class FakeWebhookDispatcher : Odisea.Application.Webhooks.IWebhookDispatcher
{
    public List<(Guid AgencyId, string EventType, object Payload)> Dispatched { get; } = [];

    public Task DispatchAsync(Guid agencyId, string eventType, object payload, CancellationToken ct)
    {
        Dispatched.Add((agencyId, eventType, payload));
        return Task.CompletedTask;
    }
}

public class FakeDevEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "TestHost";
    public string ContentRootPath { get; set; } = "/";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
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
