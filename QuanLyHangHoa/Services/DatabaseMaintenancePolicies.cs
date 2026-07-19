using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Services;

public enum DatabaseReadinessStatus
{
    Ready,
    UpgradeRequired,
    ClientUpdateRequired,
    Unavailable
}

public sealed record DatabaseReadinessSnapshot(int SchemaVersion, string MinimumClientVersion, int UpgradeCallCount);
public sealed record DatabaseReadinessResult(DatabaseReadinessStatus Status, int SchemaVersion, int UpgradeCallCount, string Code);

/// <summary>chỉ đọc metadata; startup không được tự chạy migration.</summary>
public sealed class DatabaseReadinessService
{
    private readonly Func<string, CancellationToken, Task<DatabaseReadinessSnapshot>> _read;

    public DatabaseReadinessService(Func<string, CancellationToken, Task<DatabaseReadinessSnapshot>> read) => _read = read;

    public async Task<DatabaseReadinessResult> CheckDatabaseReadyAsync(string connectionString, Version clientVersion, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _read(connectionString, cancellationToken);
            var compatibility = new DatabaseCompatibilityService().Evaluate(snapshot.SchemaVersion, snapshot.MinimumClientVersion, clientVersion.ToString());
            var status = compatibility.Status switch
            {
                DatabaseCompatibilityStatus.UpgradeRequired => DatabaseReadinessStatus.UpgradeRequired,
                DatabaseCompatibilityStatus.ClientUpdateRequired => DatabaseReadinessStatus.ClientUpdateRequired,
                _ => DatabaseReadinessStatus.Ready
            };
            return new DatabaseReadinessResult(status, snapshot.SchemaVersion, snapshot.UpgradeCallCount,
                status == DatabaseReadinessStatus.UpgradeRequired ? "DB-UPGRADE-REQUIRED" :
                status == DatabaseReadinessStatus.ClientUpdateRequired ? "DB-CLIENT-UPDATE-REQUIRED" : "READY");
        }
        catch
        {
            return new DatabaseReadinessResult(DatabaseReadinessStatus.Unavailable, 0, 0, "DB-UNAVAILABLE");
        }
    }
}

public static class ClientSessionPolicy
{
    public static TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(30);
    public static TimeSpan StaleAfter => TimeSpan.FromSeconds(90);
    public static bool IsStale(DateTimeOffset lastSeenUtc, DateTimeOffset nowUtc) => nowUtc - lastSeenUtc > StaleAfter;
}

public static class SchemaMaintenanceLock
{
    public const string SharedMode = "Shared";
    public const string ExclusiveMode = "Exclusive";
    public static string SharedResource(string database) =>
        $"WAREPRO:SCHEMAMAINTENANCE:{database.Trim().ToUpperInvariant()}";
    public static string ExclusiveResource(string database) => SharedResource(database);
}

public static class DatabaseMigrationPlan
{
    public static IReadOnlyList<string> OrderedSteps { get; } =
        ["maintenance", "sessions", "backup", "verify", "rcsi", "ddl", "metadata", "validation", "release"];
}
public sealed class ClientSessionService
{
    public static string RegisterSql => """
        MERGE dbo.__WareProClientSession AS target
        USING (SELECT @sessionId AS SessionId) AS source ON target.SessionId = source.SessionId
        WHEN MATCHED THEN UPDATE SET LastSeenUtc = SYSUTCDATETIME()
        WHEN NOT MATCHED THEN INSERT(SessionId, MachineName, ProcessId, AppVersion, StartedAtUtc, LastSeenUtc)
            VALUES(@sessionId, @machineName, @processId, @appVersion, SYSUTCDATETIME(), SYSUTCDATETIME());
        """;

    public static string HeartbeatSql => "UPDATE dbo.__WareProClientSession SET LastSeenUtc = SYSUTCDATETIME() WHERE SessionId = @sessionId;";
    public static string ReleaseSql => "DELETE FROM dbo.__WareProClientSession WHERE SessionId = @sessionId;";
}