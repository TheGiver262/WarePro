using WarePro.SetupHelper;
using WarePro.Database;
using System.Text.RegularExpressions;

namespace QuanLyHangHoa.Tests.Deployment;

public sealed class DatabaseUpgradeCommandTests
{
    [Theory]
    [InlineData(25, SetupExitCode.ActiveClients)]
    [InlineData(26, SetupExitCode.BackupFailed)]
    [InlineData(27, SetupExitCode.MigrationFailed)]
    [InlineData(28, SetupExitCode.ValidationFailed)]
    public void Upgrade_exit_codes_are_stable(int numeric, SetupExitCode expected)
    {
        Assert.Equal(expected, (SetupExitCode)numeric);
    }

    [Fact]
    public void Old_client_is_rejected_before_mutation()
    {
        var result = ClientCompatibilityPolicy.Evaluate(new Version("1.0.0"), 7);

        Assert.Equal(ClientCompatibilityStatus.Rejected, result.Status);
        Assert.Equal("1.1.0", result.MinimumClientVersion.ToString());
    }
    [Fact]
    public async Task Upgrade_database_forwards_config_version_and_schema()
    {
        var probe = new UpgradeProbe();
        var commands = new SetupCommands(probe, new NoopWriter(), () => "default.json");

        var result = await commands.ExecuteAsync([
            "upgrade-database", "--config", @"C:\ProgramData\WarePro\Config\warepro.settings.json",
            "--app-version", "1.1.0", "--expected-schema", "7"]);

        Assert.Equal(SetupExitCode.Success, result.ExitCode);
        Assert.Equal("1.1.0", probe.AppVersion?.ToString());
        Assert.Equal(7, probe.ExpectedSchema);
    }

    private sealed class UpgradeProbe : ISetupProbe
    {
        public Version? AppVersion { get; private set; }
        public int ExpectedSchema { get; private set; }
        public Task<SetupProbeResult> DetectSqlAsync(string instance, CancellationToken cancellationToken) =>
            Task.FromResult(new SetupProbeResult(SetupExitCode.Success, "ok"));
        public Task<SetupProbeResult> TestConnectionAsync(string configPath, SetupMode mode, CancellationToken cancellationToken) =>
            Task.FromResult(new SetupProbeResult(SetupExitCode.Success, "ok"));
        public Task<SetupProbeResult> UpgradeDatabaseAsync(string configPath, Version appVersion, int expectedSchema, CancellationToken cancellationToken)
        {
            AppVersion = appVersion;
            ExpectedSchema = expectedSchema;
            return Task.FromResult(new SetupProbeResult(SetupExitCode.Success, "ok"));
        }
    }

    private sealed class NoopWriter : ISetupConfigWriter
    {
        public void Save(string path, QuanLyHangHoa.Configuration.WareProSettings settings) { }
    }
    [Fact]
    public void Migration_bundle_contains_baseline_and_all_ordered_versions()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var sql = File.ReadAllText(Path.Combine(root, "WarePro.SetupHelper", "Resources", "WarePro.Migrations.sql"));
        var markers = new[]
        {
            "-- baseline", "-- SchemaMetadataSql", "-- SchemaVersion1Sql", "-- SchemaVersion2Sql",
            "-- SchemaVersion3Sql", "-- SchemaVersion4Sql", "-- SchemaVersion5Sql",
            "-- SchemaVersion6Sql", "-- SchemaVersion7Sql", "-- SchemaArchiveReplaySql"
        };
        var previous = -1;
        foreach (var marker in markers)
        {
            var index = sql.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previous, marker);
            previous = index;
        }

        var source = File.ReadAllText(Path.Combine(root, "WarePro.SetupHelper", "SetupCommands.cs"));
        Assert.Contains("BeginTransactionAsync", source, StringComparison.Ordinal);
        Assert.Contains("DatabaseSchemaScripts.BuildUpgradeSql", source, StringComparison.Ordinal);
        Assert.Contains("CommitAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Setup_helper_is_the_only_production_schema_script_runner()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var helper = File.ReadAllText(Path.Combine(root, "WarePro.SetupHelper", "SetupCommands.cs"));

        Assert.False(File.Exists(Path.Combine(root, "QuanLyHangHoa", "Services", "DatabaseInitializer.cs")));
        Assert.Contains("DatabaseSchemaScripts.BuildUpgradeSql", helper, StringComparison.Ordinal);
        Assert.Contains("DatabaseSchemaScripts.BaselineBatches", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Baseline_batches_never_create_or_switch_a_hard_coded_database()
    {
        var sql = string.Join(Environment.NewLine, DatabaseSchemaScripts.BaselineBatches);

        Assert.DoesNotContain("CREATE DATABASE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("USE [ProductManagementDb]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE dbo.AppUser", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Upgrade_sql_contains_every_version_and_stamps_metadata_last()
    {
        var sql = DatabaseSchemaScripts.BuildUpgradeSql(7, "1.1.0");

        for (var version = 3; version <= 7; version++)
            Assert.Contains($"IF @CurrentVersion < {version}", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("IF @CurrentVersion < 1", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("IF @CurrentVersion < 2", sql, StringComparison.Ordinal);

        var archive = sql.IndexOf("UX_AuditArchiveManifest_OperationId", StringComparison.Ordinal);
        var stamp = sql.LastIndexOf("SET [Version] = 7", StringComparison.Ordinal);
        Assert.True(archive >= 0 && stamp > archive);
    }

    [Fact]
    public void Upgrade_runner_uses_server_backup_directory_and_retains_verified_backup()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(root, "WarePro.SetupHelper", "SetupCommands.cs"));

        Assert.Contains("InstanceDefaultBackupPath", source, StringComparison.Ordinal);
        Assert.DoesNotContain("xp_delete_file", source, StringComparison.Ordinal);
        Assert.Contains("Verified backup retained", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.GetTempPath()", source, StringComparison.Ordinal);
    }
    [Fact]
    public void Upgrade_sql_never_sends_client_go_delimiters_to_sql_server()
    {
        var sql = DatabaseSchemaScripts.BuildUpgradeSql(7, "1.1.0");
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*GO\s*(?:--.*)?$"), sql);
        Assert.All(DatabaseSchemaScripts.BaselineBatches,
            batch => Assert.DoesNotMatch(new Regex(@"(?im)^\s*GO\s*(?:--.*)?$"), batch));
    }

    [Fact]
    public void Legacy_transfer_types_defaults_and_nullability_are_repaired_before_validation()
    {
        var sql = DatabaseSchemaScripts.BuildUpgradeSql(7, "1.1.0");
        var validation = sql.IndexOf("WarePro schema shape validation failed", StringComparison.Ordinal);
        foreach (var repair in new[]
                 {
                     "ALTER COLUMN TransferDate DATETIME2(0) NOT NULL",
                     "ALTER COLUMN CreatedAt DATETIME2(0) NOT NULL",
                     "ALTER COLUMN Notes NVARCHAR(500) NULL",
                     "DF_StockTransfer_CreatedAt"
                 })
        {
            var index = sql.IndexOf(repair, StringComparison.OrdinalIgnoreCase);
            Assert.True(index >= 0 && index < validation, repair);
        }
    }

    [Fact]
    public void Exact_shape_contract_covers_metadata_sessions_and_every_mutable_rowversion()
    {
        var predicate = DatabaseSchemaScripts.ShapeValidationPredicate;
        Assert.DoesNotContain("N'datetime2', 8, 27, 0", predicate, StringComparison.Ordinal);
        foreach (var marker in new[]
                 {
                     "__WareProDatabaseIdentity", "__WareProSchemaVersion", "MinimumClientVersion",
                     "__WareProClientSession", "SessionId", "LastSeenUtc", "AppUser", "Product",
                     "StockBalance", "StockLedger", "PurchaseInvoice", "SalesInvoice",
                     "StockTransfer", "WarrantyClaim", "RowVersion", "IX___WareProClientSession_LastSeenUtc",
                     "CategoryId", "DefaultUnitId", "GrandTotal", "PaidAmount", "PaymentStatus",
                     "FK_Product_Category", "FK_StockInLine_StockIn",
                     "CK_Product_DefaultPrice_NonNegative", "CK_PurchaseInvoice_PaymentStatus",
                     "IX_PurchaseInvoice_PaymentStatus_InvoiceDate", "UX_StockBalance_Warehouse_Product"
                 })
            Assert.Contains(marker, predicate, StringComparison.Ordinal);
    }
}