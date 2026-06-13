using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Odisea.Application.Suppliers.Dtos;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Data;
using Odisea.UnitTests.Helpers;
using Odisea.WebAPI.Controllers;

namespace Odisea.UnitTests.Suppliers;

// Guards tenant isolation on operator→agency entitlements (commission terms are
// sensitive): an operator must only ever see/touch its own.
public class EntitlementsControllerScopingTests
{
    private static AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    // operatorId == null simulates a platform admin (no operator tenant).
    private static EntitlementsController ControllerFor(AppDbContext db, Guid? operatorId) =>
        new(db, new FakeOperatorContext(operatorId));

    private static async Task<OperatorAgencyEntitlement> Seed(AppDbContext db, Guid operatorId, Guid agencyId)
    {
        var e = new OperatorAgencyEntitlement
        {
            OperatorId = operatorId,
            AgencyId = agencyId,
            CommissionPercent = 12m,
            Status = EntitlementStatus.Active,
        };
        db.Entitlements.Add(e);
        await db.SaveChangesAsync();
        return e;
    }

    [Fact]
    public async Task List_returnsOnlyCallingOperatorsEntitlements()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        await Seed(db, mine, Guid.NewGuid());
        await Seed(db, Guid.NewGuid(), Guid.NewGuid()); // another operator's deal

        var result = await ControllerFor(db, mine).List(null, null, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EntitlementDto>>(ok.Value);
        Assert.Single(dtos);
        Assert.All(dtos, d => Assert.Equal(mine, d.OperatorId));
    }

    [Fact]
    public async Task List_ignoresOperatorIdQueryParam_whenCallerIsAnOperator()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        await Seed(db, mine, Guid.NewGuid());
        await Seed(db, theirs, Guid.NewGuid());

        // An operator tries to peek at another operator's deals via the query param.
        var result = await ControllerFor(db, mine).List(operatorId: theirs, agencyId: null, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EntitlementDto>>(ok.Value);
        Assert.All(dtos, d => Assert.Equal(mine, d.OperatorId)); // forced to caller, theirs not leaked
    }

    [Fact]
    public async Task List_platformAdmin_seesAll()
    {
        await using var db = NewDb();
        await Seed(db, Guid.NewGuid(), Guid.NewGuid());
        await Seed(db, Guid.NewGuid(), Guid.NewGuid());

        var result = await ControllerFor(db, operatorId: null).List(null, null, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<EntitlementDto>>(ok.Value);
        Assert.Equal(2, dtos.Count());
    }

    [Fact]
    public async Task Create_forAnotherOperator_returns403()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var agency = Guid.NewGuid();
        db.Operators.Add(new Operator { Id = Guid.NewGuid(), Name = "Other", Slug = "other" });
        db.Agencies.Add(new Agency { Id = agency, Name = "A", Slug = "a" });
        await db.SaveChangesAsync();

        var req = new CreateEntitlementRequest(OperatorId: Guid.NewGuid(), AgencyId: agency, CommissionPercent: 10m);
        var result = await ControllerFor(db, mine).Create(req, default);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, problem.StatusCode);
        Assert.Equal(0, await db.Entitlements.CountAsync());
    }

    [Fact]
    public async Task Suspend_anotherOperatorsEntitlement_returns404_andLeavesItActive()
    {
        await using var db = NewDb();
        var theirs = await Seed(db, Guid.NewGuid(), Guid.NewGuid());

        var result = await ControllerFor(db, Guid.NewGuid()).Suspend(theirs.Id, default);

        Assert.IsType<NotFoundResult>(result);
        var reloaded = await db.Entitlements.FirstAsync(e => e.Id == theirs.Id);
        Assert.Equal(EntitlementStatus.Active, reloaded.Status); // untouched
    }

    [Fact]
    public async Task Suspend_ownEntitlement_succeeds()
    {
        await using var db = NewDb();
        var mine = Guid.NewGuid();
        var e = await Seed(db, mine, Guid.NewGuid());

        var result = await ControllerFor(db, mine).Suspend(e.Id, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(EntitlementStatus.Suspended, (await db.Entitlements.FirstAsync()).Status);
    }
}
