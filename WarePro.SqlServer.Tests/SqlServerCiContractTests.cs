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
        var upgrade = DatabaseSchemaScripts.BuildUpgradeSql(6, "1.1.0");
        for (var version = 1; version <= 6; version++)
            Assert.Contains($"IF @CurrentVersion < {version} BEGIN EXEC sys.sp_executesql N'", upgrade, StringComparison.Ordinal);

        Assert.All(DatabaseSchemaScripts.BaselineBatches, AssertHasNoGoBatch);
        AssertHasNoGoBatch(upgrade);
        AssertHasNoGoBatch(DatabaseSchemaScripts.BuildFinalizeSql(6, "1.1.0"));
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
    private static void AssertHasNoGoBatch(string sql) =>
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*GO\s*(?:--.*)?$"), sql);

    private static string Normalize(string sql) => sql.Trim();
}
