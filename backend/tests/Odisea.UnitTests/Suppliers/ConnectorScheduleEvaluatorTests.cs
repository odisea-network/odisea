using Odisea.Application.Suppliers.Connectors;
using Odisea.Domain.Entities;
using Odisea.Domain.Enums;

namespace Odisea.UnitTests.Suppliers;

public class ConnectorScheduleEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan SixHours = TimeSpan.FromHours(6);

    private static SupplierConnection Conn(
        SupplierConnectionStatus status = SupplierConnectionStatus.Active,
        DateTime? lastSynced = null) =>
        new() { Name = "c", Status = status, LastSyncedAt = lastSynced };

    [Fact]
    public void NeverSynced_active_isDue()
    {
        Assert.True(ConnectorScheduleEvaluator.IsDue(Conn(lastSynced: null), Now, SixHours));
    }

    [Fact]
    public void SyncedLongerAgoThanInterval_isDue()
    {
        var conn = Conn(lastSynced: Now.AddHours(-7));
        Assert.True(ConnectorScheduleEvaluator.IsDue(conn, Now, SixHours));
    }

    [Fact]
    public void SyncedWithinInterval_isNotDue()
    {
        var conn = Conn(lastSynced: Now.AddHours(-1));
        Assert.False(ConnectorScheduleEvaluator.IsDue(conn, Now, SixHours));
    }

    [Fact]
    public void SyncedExactlyAtInterval_isDue()
    {
        var conn = Conn(lastSynced: Now.AddHours(-6));
        Assert.True(ConnectorScheduleEvaluator.IsDue(conn, Now, SixHours));
    }

    [Theory]
    [InlineData(SupplierConnectionStatus.Paused)]
    [InlineData(SupplierConnectionStatus.Failed)]
    public void NonActive_isNeverDue_evenIfStale(SupplierConnectionStatus status)
    {
        var conn = Conn(status, lastSynced: null);
        Assert.False(ConnectorScheduleEvaluator.IsDue(conn, Now, SixHours));
    }

    [Fact]
    public void DueConnections_filtersToOnlyTheDueActiveOnes()
    {
        var connections = new[]
        {
            Conn(lastSynced: null),                                     // due
            Conn(lastSynced: Now.AddHours(-1)),                        // fresh
            Conn(lastSynced: Now.AddHours(-8)),                        // stale → due
            Conn(SupplierConnectionStatus.Paused, lastSynced: null),  // paused
        };

        var due = ConnectorScheduleEvaluator.DueConnections(connections, Now, SixHours).ToList();

        Assert.Equal(2, due.Count);
    }
}
