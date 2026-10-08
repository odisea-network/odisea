using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odisea.Modules.Integrations.Adapters.Mock;
using Odisea.Modules.Integrations.Features;
using Odisea.Modules.Integrations.Infrastructure;
using Odisea.Modules.Integrations.PublicApi;
using Odisea.SharedKernel;
using Xunit;

namespace Odisea.UnitTests.Integrations;

public class ProviderRegistryTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.UtcNow;
    }

    private static IntegrationsDbContext InMemoryDb() => new(
        new DbContextOptionsBuilder<IntegrationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Unknown_provider_code_throws()
    {
        var registry = new ProviderRegistry([], InMemoryDb());
        Assert.Throws<UnknownProviderException>(() => registry.Get("tourvisio"));
    }

    [Fact]
    public async Task Calls_through_the_registry_leave_a_call_log()
    {
        var db = InMemoryDb();
        var mock = new MockReservationProvider(Options.Create(new MockProviderOptions()), new FakeClock());
        var registry = new ProviderRegistry([mock], db);

        var provider = registry.Get("MOCK"); // case-insensitive
        await provider.SearchAsync(
            new ProviderSearchRequest("ANT", new DateRange(new DateOnly(2027, 3, 30), new DateOnly(2027, 4, 6)), new Pax(2), []),
            CancellationToken.None);

        var log = Assert.Single(db.ProviderCallLogs);
        Assert.Equal("mock", log.ProviderCode);
        Assert.Equal("Search", log.Operation);
        Assert.True(log.Success);
        Assert.Contains("offers=6", log.Detail);
    }
}
