using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;
using Odisea.Infrastructure.Suppliers.Connectors;

namespace Odisea.UnitTests.Suppliers;

public class ConnectorRegistryTests
{
    private sealed class FakeXmlConnector : IConnector
    {
        public SupplierConnectionKind Kind => SupplierConnectionKind.Xml;
        public Task<ConnectorRunResult> RunAsync(SupplierConnection c, CancellationToken ct) =>
            Task.FromResult(new ConnectorRunResult(42, 40, 2, [], DateTime.UtcNow));
    }

    [Fact]
    public void For_KnownKind_ReturnsRegisteredConnector()
    {
        var registry = new ConnectorRegistry(new IConnector[]
        {
            new ManualConnector(),
            new FakeXmlConnector(),
        });

        Assert.IsType<ManualConnector>(registry.For(SupplierConnectionKind.Manual));
        Assert.IsType<FakeXmlConnector>(registry.For(SupplierConnectionKind.Xml));
    }

    [Fact]
    public void For_UnknownKind_ThrowsNotSupported()
    {
        var registry = new ConnectorRegistry(new IConnector[] { new ManualConnector() });

        var ex = Assert.Throws<NotSupportedException>(
            () => registry.For(SupplierConnectionKind.JsonApi));

        Assert.Contains("JsonApi", ex.Message);
    }

    [Fact]
    public async Task ManualConnector_RunAsync_ReturnsEmptySucceedingResult()
    {
        var connector = new ManualConnector();
        var connection = new SupplierConnection { Kind = SupplierConnectionKind.Manual };

        var result = await connector.RunAsync(connection, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.OffersFetched);
        Assert.Equal(0, result.OffersImported);
        Assert.Equal(0, result.OffersDeactivated);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ConnectorRunResult_Empty_WithReason_FailsAndCarriesError()
    {
        var result = ConnectorRunResult.Empty("connection paused");

        Assert.False(result.Succeeded);
        Assert.Single(result.Errors);
        Assert.Equal("connection paused", result.Errors[0]);
    }
}
