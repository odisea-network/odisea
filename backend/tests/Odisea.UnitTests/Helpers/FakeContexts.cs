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
