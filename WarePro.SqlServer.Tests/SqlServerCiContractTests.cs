using System.Text.RegularExpressions;
using WarePro.Database;

namespace WarePro.SqlServer.Tests;

public sealed class SqlServerCiContractTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Shared_migration_commands_are_ordered_and_go_free()
    {
        var upgrade = DatabaseSchemaScripts.BuildUpgradeSql(8, "1.1.0");
        for (var version = 3; version <= 8; version++)
            Assert.Contains($"IF @CurrentVersion < {version} BEGIN EXEC sys.sp_executesql N'", upgrade, StringComparison.Ordinal);
        Assert.DoesNotContain("IF @CurrentVersion < 1", upgrade, StringComparison.Ordinal);
        Assert.DoesNotContain("IF @CurrentVersion < 2", upgrade, StringComparison.Ordinal);
        Assert.True(upgrade.IndexOf("COL_LENGTH(''SalesInvoice'', ''PaidAmount'')", StringComparison.Ordinal) < upgrade.IndexOf("UPDATE dbo.SalesInvoice SET PaidAmount", StringComparison.Ordinal));

        Assert.All(DatabaseSchemaScripts.BaselineBatches, AssertHasNoGoBatch);
        AssertHasNoGoBatch(upgrade);
        AssertHasNoGoBatch(DatabaseSchemaScripts.BuildFinalizeSql(8, "1.1.0"));
        Assert.Matches(new Regex(@"(?i)\[RowCount\]\s+INT\s+NOT\s+NULL"), string.Join('\n', DatabaseSchemaScripts.BaselineBatches));
        Assert.Matches(new Regex(@"(?i)\[RowCount\]\s+INT\s+NOT\s+NULL"), upgrade);
    }

    [Fact]
    public void Solution_runner_and_workflow_execute_the_cross_platform_sql_tests()
    {
        var solution = File.ReadAllText(Path.Combine(Root, "QuanLyHangHoa", "QuanLyHangHoa.sln"));
        Assert.Contains("WarePro.SqlServer.Tests", solution, StringComparison.Ordinal);

        var runner = File.ReadAllText(Path.Combine(Root, "scripts", "test", "Invoke-WareProSqlServerTests.ps1"));
        Assert.Contains("WarePro.SqlServer.Tests.csproj", runner, StringComparison.Ordinal);
        Assert.Contains(SqlServerTestDatabase.ConnectionStringVariable, runner, StringComparison.Ordinal);

        var workflow = File.ReadAllText(Path.Combine(Root, ".github", "workflows", "warepro-sqlserver.yml"));
        foreach (var marker in new[]
                 {
                     "ubuntu-latest", "mcr.microsoft.com/mssql/server:2022-latest", "openssl rand",
                     "add-mask", "sqlcmd", "dotnet test", "WarePro.SqlServer.Tests.csproj", "if: always()",
                     "docker rm -f"
                 })
            Assert.Contains(marker, workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_version_6_comes_from_checked_in_common_write_safety_script()
    {
        var canonical = File.ReadAllText(Path.Combine(
            Root, "Database", "Schema", "v6-common-write-safety.sql"));

        Assert.Equal(Normalize(canonical), Normalize(DatabaseSchemaScripts.SchemaVersion6));
    }

    [Fact]
    public void Schema_version_7_comes_from_checked_in_invoice_void_script()
    {
        var canonical = File.ReadAllText(Path.Combine(
            Root, "Database", "Schema", "v7-invoice-void-open-claim.sql"));

        Assert.Equal(Normalize(canonical), Normalize(DatabaseSchemaScripts.SchemaVersion7));
        Assert.Contains("OpenProductSerialId", canonical, StringComparison.Ordinal);
        Assert.Contains("UX_WarrantyClaim_OpenProductSerialId", canonical, StringComparison.Ordinal);
        Assert.Contains("ON dbo.WarrantyClaim(ProductSerialId)", canonical, StringComparison.Ordinal);
        Assert.Contains("WHERE [Status] <> N''Closed'' AND [Status] <> N''Rejected''", canonical, StringComparison.Ordinal);
    }
    private static void AssertHasNoGoBatch(string sql) =>
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*GO\s*(?:--.*)?$"), sql);

    private static string Normalize(string sql) => sql.Trim();
}
