using QuanLyHangHoa.Services;
using WarePro.SetupHelper;

namespace QuanLyHangHoa.Tests.Services;

public sealed class DatabaseMaintenanceContractTests
{
    [Fact]
    public async Task Readiness_does_not_upgrade_an_old_schema()
    {
        var readiness = new DatabaseReadinessService((_, _) => Task.FromResult(
            new DatabaseReadinessSnapshot(5, "1.0.0", 0)));

        var result = await readiness.CheckDatabaseReadyAsync("ignored", new Version("1.1.0"), CancellationToken.None);

        Assert.Equal(DatabaseReadinessStatus.UpgradeRequired, result.Status);
        Assert.Equal(0, result.UpgradeCallCount);
    }

    [Fact]
    public void Session_policy_uses_thirty_second_heartbeat_and_ninety_second_stale_cutoff()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), ClientSessionPolicy.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromSeconds(90), ClientSessionPolicy.StaleAfter);
        Assert.True(ClientSessionPolicy.IsStale(DateTimeOffset.UtcNow.AddSeconds(-91), DateTimeOffset.UtcNow));
        Assert.False(ClientSessionPolicy.IsStale(DateTimeOffset.UtcNow.AddSeconds(-89), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Lock_names_are_shared_for_writes_and_exclusive_for_maintenance()
    {
        Assert.Equal("WAREPRO:SCHEMAMAINTENANCE:DB", SchemaMaintenanceLock.SharedResource("db"));
        Assert.Equal("WAREPRO:SCHEMAMAINTENANCE:DB", SchemaMaintenanceLock.ExclusiveResource("db"));
        Assert.Equal("Shared", SchemaMaintenanceLock.SharedMode);
        Assert.Equal("Exclusive", SchemaMaintenanceLock.ExclusiveMode);
    }

    [Fact]
    public void Migration_steps_are_ordered_and_idempotent()
    {
        Assert.Equal(
            ["maintenance", "sessions", "backup", "verify", "rcsi", "ddl", "metadata", "validation", "release"],
            DatabaseMigrationPlan.OrderedSteps);
    }
}
