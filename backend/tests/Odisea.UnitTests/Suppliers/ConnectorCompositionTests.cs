using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Enums;
using Odisea.Infrastructure;

namespace Odisea.UnitTests.Suppliers;

// Guards the connector DI wiring: the registry is scoped and composes a singleton
// (Manual) plus a scoped, DbContext-backed adapter (JsonApi). A captive-dependency
// regression (registry back to singleton) would throw here under scope validation.
public class ConnectorCompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Port=5433;Database=odisea;Username=odisea;Password=odisea",
                ["Jwt:Secret"] = "test-secret-that-is-at-least-32-chars-long",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config); // the real host provides this
        services.AddInfrastructureServices(config);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    [Fact]
    public void Registry_resolvesInScope_andServesBothConnectors()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<IConnectorRegistry>();

        Assert.Equal(SupplierConnectionKind.Manual, registry.For(SupplierConnectionKind.Manual).Kind);
        Assert.Equal(SupplierConnectionKind.JsonApi, registry.For(SupplierConnectionKind.JsonApi).Kind);
    }

    [Fact]
    public void Registry_unknownKind_throwsNotSupported()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var registry = scope.ServiceProvider.GetRequiredService<IConnectorRegistry>();

        Assert.Throws<NotSupportedException>(() => registry.For(SupplierConnectionKind.CsvSftp));
    }
}
