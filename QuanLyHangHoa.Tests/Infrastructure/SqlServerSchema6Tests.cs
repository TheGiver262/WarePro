using System.Data;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.Tests.Infrastructure;

public sealed class SqlServerSchema6Tests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [SqlServerConcurrencyFact]
    [Trait("Category", "SqlServerConcurrency")]
    public async Task Schema_6_runs_twice_and_validates_database_state()
    {
        await using var database = SqlServerTestDatabase.FromEnvironment();
        await database.InitializeAsync();

        var sql = await File.ReadAllTextAsync(Path.Combine(
            RepoRoot,
            "Database",
            "Schema",
            "v6-common-write-safety.sql"));

        string connectionString;
        await using (var context = database.CreateContext())
        {
            await context.Database.EnsureCreatedAsync();
            await context.Database.ExecuteSqlRawAsync(sql);
            await context.Database.ExecuteSqlRawAsync(sql);
            connectionString = context.Database.GetConnectionString()!;
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT [is_read_committed_snapshot_on]
                 FROM sys.databases WHERE [name] = DB_NAME()),
                (SELECT [Version] FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1),
                (SELECT [MinimumClientVersion] FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1),
                CASE WHEN OBJECT_ID(N'[dbo].[__WareProClientSession]', N'U') IS NULL THEN 0 ELSE 1 END,
                (SELECT COUNT(*)
                 FROM sys.columns
                 WHERE [name] = N'RowVersion'
                   AND [system_type_id] = TYPE_ID(N'timestamp'));
            """;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal(6, reader.GetInt32(1));
        Assert.Equal("1.1.0", reader.GetString(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.Equal(29, reader.GetInt32(4));
    }
}
