using System.IO;
using System.Reflection;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class DatabaseInitializerSchemaTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Current_schema_repairs_warehouse_posting_columns()
    {
        var type = typeof(DatabaseInitializer);
        var versionField = type.GetField("CurrentSchemaVersion", BindingFlags.NonPublic | BindingFlags.Static);
        var sqlField = type.GetField("SchemaVersion4Sql", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(versionField);
        Assert.NotNull(sqlField);
        var version = (int)versionField.GetRawConstantValue()!;
        var sql = (string)sqlField.GetRawConstantValue()!;

        Assert.Equal(5, version);
        foreach (var table in new[] { "StockIn", "StockOut", "StockAdjustment", "StockTransfer" })
        {
            Assert.Contains($"COL_LENGTH('{table}', 'ApprovedAt')", sql);
            Assert.Contains($"COL_LENGTH('{table}', 'PostedAt')", sql);
        }
    }

    [Fact]
    public void Schema_version_table_records_client_and_application_compatibility()
    {
        var sqlField = typeof(DatabaseInitializer).GetField(
            "SchemaMetadataSql",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(sqlField);
        var sql = (string)sqlField.GetRawConstantValue()!;
        Assert.Contains("MinimumClientVersion", sql, StringComparison.Ordinal);
        Assert.Contains("AppliedByAppVersion", sql, StringComparison.Ordinal);
        Assert.Contains("UpdatedAt", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Upgrade_flow_locks_and_verifies_backup_before_running_transactional_DDL()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Services", "DatabaseInitializer.cs"));

        var lockIndex = source.IndexOf("SchemaUpgradeLock.Acquire", StringComparison.Ordinal);
        var ensureCreatedIndex = source.IndexOf("db.Database.EnsureCreated()", StringComparison.Ordinal);
        var backupIndex = source.IndexOf(".CreateAndVerify(", lockIndex, StringComparison.Ordinal);
        var applyIndex = source.IndexOf("ApplySchemaUpdates(db)", backupIndex, StringComparison.Ordinal);
        var seedIndex = source.IndexOf("SeedIfNeeded(db)", applyIndex, StringComparison.Ordinal);
        var enclosingFinallyIndex = source.IndexOf("finally", lockIndex, StringComparison.Ordinal);

        Assert.True(lockIndex >= 0, "Schema upgrade lock is missing.");
        Assert.True(ensureCreatedIndex > lockIndex, "The lock must be held before EnsureCreated changes the database.");
        Assert.True(backupIndex > lockIndex, "Backup must run while the schema lock is held.");
        Assert.True(applyIndex > backupIndex, "DDL must run only after backup verification.");
        Assert.True(seedIndex > applyIndex, "Seed must run after the schema is ready.");
        Assert.True(seedIndex < enclosingFinallyIndex, "The schema lock must stay held until seeding finishes.");
        Assert.Contains("hasExistingBusinessTables", source, StringComparison.Ordinal);
        Assert.Contains("sys.tables", source, StringComparison.Ordinal);
        Assert.Contains("BeginTransaction", source, StringComparison.Ordinal);
    }
}
