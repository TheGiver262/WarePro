using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using WarePro.Database;

namespace WarePro.SqlServer.Tests;

public sealed class SchemaMigrationTests
{
    private static readonly string[] MutableTables =
    [
        "AppUser", "AuditArchiveManifest", "Brand", "Category", "Customer", "Product",
        "ProductSerial", "ProductUnit", "PurchaseInvoice", "PurchaseInvoiceLine", "SalesInvoice",
        "SalesInvoiceLine", "StockAdjustment", "StockAdjustmentLine", "StockBalance", "StockCountLine",
        "StockCountSession", "StockIn", "StockInLine", "StockOut", "StockOutLine",
        "StockTransfer", "StockTransferLine", "Supplier", "Unit", "Warehouse", "WarrantyClaim",
        "WarrantyCoverage"
    ];

    [SqlServerFact]
    [Trait("Category", "RealDatabase")]
    public async Task Fresh_database_runs_shared_schema_twice_and_matches_contract()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();

        await database.ApplyBaselineAsync();
await database.ApplyUpgradeAsync();
        await database.ApplyUpgradeAsync();
        await database.ApplyFinalizeAsync();
        await database.ApplyFinalizeAsync();

        Assert.All(DatabaseSchemaScripts.BaselineBatches, AssertHasNoGoBatch);
        AssertHasNoGoBatch(DatabaseSchemaScripts.BuildUpgradeSql(6, "1.1.0"));

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()),
                (SELECT Version FROM dbo.__WareProSchemaVersion WHERE Id = 1),
                (SELECT MinimumClientVersion FROM dbo.__WareProSchemaVersion WHERE Id = 1),
                CASE WHEN EXISTS
                (
                    SELECT 1 FROM dbo.__WareProDatabaseIdentity
                    WHERE Id = 1
                      AND ProductId = 'F65EAB95-A3F8-4D8D-9AF5-4839FCA38E21'
                      AND ProductName = N'WarePro'
                ) THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.AuditArchiveManifest', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.__WareProClientSession', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN ({DatabaseSchemaScripts.ShapeValidationPredicate}) THEN 1 ELSE 0 END;
            """;

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.Equal(6, reader.GetInt32(1));
        Assert.Equal("1.1.0", reader.GetString(2));
        Assert.Equal(1, reader.GetInt32(3));
        Assert.Equal(1, reader.GetInt32(4));
        Assert.Equal(1, reader.GetInt32(5));
        Assert.Equal(1, reader.GetInt32(6));

        await reader.DisposeAsync();
        await using var rowVersionCommand = connection.CreateCommand();
        rowVersionCommand.CommandText = """
            SELECT OBJECT_NAME(object_id)
            FROM sys.columns
            WHERE name = N'RowVersion'
              AND TYPE_NAME(system_type_id) = N'timestamp'
              AND is_nullable = 0;
            """;
        var actual = new HashSet<string>(StringComparer.Ordinal);
        await using var rowVersionReader = await rowVersionCommand.ExecuteReaderAsync();
        while (await rowVersionReader.ReadAsync())
            actual.Add(rowVersionReader.GetString(0));
        Assert.All(MutableTables, table => Assert.Contains(table, actual));
    }

    [SqlServerFact]
    [Trait("Category", "RealDatabase")]
    public async Task Canonical_v6_script_runs_directly_twice()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.ApplyBaselineAsync();
        await database.ApplyCanonicalV6Async();
        await database.ApplyCanonicalV6Async();

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT CASE WHEN (SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()) = 1
                AND (SELECT Version FROM dbo.__WareProSchemaVersion WHERE Id = 1) = 6
                AND (SELECT MinimumClientVersion FROM dbo.__WareProSchemaVersion WHERE Id = 1) = N'1.1.0'
                AND OBJECT_ID(N'dbo.__WareProClientSession', N'U') IS NOT NULL
                AND COL_LENGTH(N'dbo.Category', N'RowVersion') IS NOT NULL THEN 1 ELSE 0 END;
            """;
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));
    }
    [SqlServerTheory]
    [Trait("Category", "RealDatabase")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Legacy_versions_and_transfer_shape_upgrade_to_schema_6(int currentVersion)
    {
        await using var database = await SqlServerTestDatabase.CreateLegacyAsync(currentVersion);
        await AssertHistoricalVersionMarkerAsync(database, currentVersion);
        await database.ApplyLegacyTransferShapeAsync();
        await database.ApplyUpgradeAsync();
        await database.ApplyUpgradeAsync();

        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT CASE WHEN
                (SELECT Version FROM dbo.__WareProSchemaVersion WHERE Id = 1) = 6
                AND ({DatabaseSchemaScripts.ShapeValidationPredicate})
                THEN 1 ELSE 0 END;
            """;
        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync()));

        await using var metadata = connection.CreateCommand();
        metadata.CommandText = """
            SELECT MinimumClientVersion, AppliedByAppVersion
            FROM dbo.__WareProSchemaVersion
            WHERE Id = 1;
            """;
        await using var reader = await metadata.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync());
        Assert.Equal("1.1.0", reader.GetString(0));
        Assert.Equal("1.1.0", reader.GetString(1));
    }

    [SqlServerFact]
    [Trait("Category", "RealDatabase")]
    public async Task Cleanup_drops_created_database_and_rejects_unguarded_names()
    {
        var database = await SqlServerTestDatabase.CreateAsync();
        var databaseName = database.DatabaseName;

        await database.DisposeAsync();

        Assert.Equal(databaseName, database.DatabaseName);
        Assert.Null(await database.GetDatabaseIdAsync());
        Assert.Throws<InvalidOperationException>(
            () => SqlServerTestDatabase.ValidateDatabaseName("ProductManagementDb"));
    }
    [SqlServerFact]
    [Trait("Category", "RealDatabase")]
    public async Task Rowversion_rejects_a_stale_update()
    {
        await using var database = await SqlServerTestDatabase.CreateMigratedAsync();
        await using var connection = await database.OpenConnectionAsync(clientSchema: 6);

        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT dbo.Category (CategoryCode, DisplayName, IsActive)
            OUTPUT INSERTED.Id, INSERTED.RowVersion
            VALUES (N'RV-TEST', N'Before', 1);
            """;
        await using var reader = await insert.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync());
        var id = reader.GetInt32(0);
        var originalRowVersion = (byte[])reader[1];
        await reader.DisposeAsync();

        await using var firstUpdate = CreateStaleUpdate(connection, id, originalRowVersion, "First");
        Assert.Equal(1, await firstUpdate.ExecuteNonQueryAsync());
        await using var staleUpdate = CreateStaleUpdate(connection, id, originalRowVersion, "Stale");
        Assert.Equal(0, await staleUpdate.ExecuteNonQueryAsync());
    }

    [SqlServerFact]
    [Trait("Category", "RealDatabase")]
    public async Task Finalized_write_gate_rejects_old_client_and_accepts_current_client()
    {
        await using var database = await SqlServerTestDatabase.CreateMigratedAsync();
        await database.ApplyFinalizeAsync();
        await database.ApplyFinalizeAsync();

        await using (var oldClient = await database.OpenConnectionAsync(clientSchema: 5))
        await using (var oldWrite = oldClient.CreateCommand())
        {
            oldWrite.CommandText = "INSERT dbo.Category (CategoryCode, DisplayName, IsActive) VALUES (N'OLD', N'Old', 1);";
            var error = await Assert.ThrowsAsync<SqlException>(() => oldWrite.ExecuteNonQueryAsync());
            Assert.Equal(51006, error.Number);
        }

        await using var currentClient = await database.OpenConnectionAsync(clientSchema: 6);
        await using var currentWrite = currentClient.CreateCommand();
        currentWrite.CommandText = "INSERT dbo.Category (CategoryCode, DisplayName, IsActive) VALUES (N'CURRENT', N'Current', 1);";
        Assert.Equal(1, await currentWrite.ExecuteNonQueryAsync());
    }

    private static async Task AssertHistoricalVersionMarkerAsync(SqlServerTestDatabase database, int version)
    {
        await using var connection = await database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                CASE WHEN COL_LENGTH(N'dbo.StockIn', N'ApprovedAt') IS NULL THEN 1 ELSE 0 END,
                CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductSerial') AND name = N'IX_ProductSerial_Product_Warehouse_Status') THEN 1 ELSE 0 END,
                CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.WarrantyClaim') AND name = N'IX_WarrantyClaim_ProductSerialId') THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.CK_SalesInvoice_PaymentStatus', N'C') IS NULL THEN 1 ELSE 0 END;
            """;
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
        Assert.True(await reader.ReadAsync());
        Assert.Equal(version < 4, reader.GetInt32(0) == 1);
        Assert.Equal(version >= 2, reader.GetInt32(1) == 1);
        Assert.Equal(version >= 3, reader.GetInt32(2) == 1);
        Assert.Equal(version < 5, reader.GetInt32(3) == 1);
    }
    private static SqlCommand CreateStaleUpdate(
        SqlConnection connection,
        int id,
        byte[] rowVersion,
        string displayName)
    {
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Category
            SET DisplayName = @displayName
            WHERE Id = @id AND RowVersion = @rowVersion;
            """;
        command.Parameters.AddWithValue("@displayName", displayName);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.Add("@rowVersion", SqlDbType.Timestamp, 8).Value = rowVersion;
        return command;
    }

    private static void AssertHasNoGoBatch(string sql) =>
        Assert.DoesNotMatch(new Regex(@"(?im)^\s*GO\s*(?:--.*)?$"), sql);
}
