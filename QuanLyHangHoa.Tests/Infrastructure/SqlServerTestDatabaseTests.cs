using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Tests.Infrastructure;

public sealed class SqlServerTestDatabaseTests
{
    [SqlServerConcurrencyFact]
    [Trait("Category", "SqlServerConcurrency")]
    public async Task InitializeAsync_CreatesUniqueDatabaseThatAcceptsConnections()
    {
        await using var database = SqlServerTestDatabase.FromEnvironment();
        await using var secondDatabase = SqlServerTestDatabase.FromEnvironment();

        Assert.StartsWith("WarePro_ConcurrencyTest_", database.DatabaseName, StringComparison.Ordinal);
        Assert.NotEqual(database.DatabaseName, secondDatabase.DatabaseName);

        await database.InitializeAsync();

        await using (var context = database.CreateContext())
        {
            Assert.True(await context.Database.CanConnectAsync());
        }

        await database.DisposeAsync();

        await using var masterConnection = new SqlConnection(
            Environment.GetEnvironmentVariable(
                SqlServerTestDatabase.ConnectionStringEnvironmentVariable));
        await masterConnection.OpenAsync();

        await using var command = masterConnection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@databaseName);";
        command.Parameters.AddWithValue("@databaseName", database.DatabaseName);

        Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SqlServerConcurrencyFactAttribute : FactAttribute
{
    public SqlServerConcurrencyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable(
                    SqlServerTestDatabase.ConnectionStringEnvironmentVariable)))
        {
            Skip =
                $"Set {SqlServerTestDatabase.ConnectionStringEnvironmentVariable} to a disposable SQL Server master database.";
        }
    }
}