using System.Data;
using Microsoft.Data.SqlClient;
using WarePro.Database;

namespace WarePro.SqlServer.Tests;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    internal const string ConnectionStringVariable = "WAREPRO_SQLSERVER_TEST_CONNECTION_STRING";
    private const string DatabasePrefix = "WarePro_SchemaTest_";
    private readonly string _masterConnectionString;
    private readonly string _databaseConnectionString;
    private bool _disposed;

    private SqlServerTestDatabase(
        string masterConnectionString,
        string databaseConnectionString,
        string databaseName)
    {
        _masterConnectionString = masterConnectionString;
        _databaseConnectionString = databaseConnectionString;
        DatabaseName = databaseName;
    }

    internal string DatabaseName { get; }

    internal static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var raw = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException($"Set {ConnectionStringVariable} to the disposable SQL Server master database.");

        var master = new SqlConnectionStringBuilder(raw);
        if (!string.Equals(master.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{ConnectionStringVariable} must target master.");

        var databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
        ValidateDatabaseName(databaseName);
        master.Pooling = false;
        master.ApplicationName = "WarePro.SqlServer.Tests";
        var target = new SqlConnectionStringBuilder(master.ConnectionString)
        {
            InitialCatalog = databaseName,
            Pooling = false
        };
        var database = new SqlServerTestDatabase(master.ConnectionString, target.ConnectionString, databaseName);

        await using var connection = new SqlConnection(database._masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@name); EXEC sys.sp_executesql @sql;";
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = databaseName;
        await command.ExecuteNonQueryAsync();
        return database;
    }

    internal static async Task<SqlServerTestDatabase> CreateMigratedAsync()
    {
        var database = await CreateAsync();
        try
        {
            await database.ApplyBaselineAsync();
            await database.ApplyUpgradeAsync();
            await database.ApplyUpgradeAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    internal static async Task<SqlServerTestDatabase> CreateLegacyAsync(int version)
    {
        var database = await CreateAsync();
        try
        {
            await database.ApplyBaselineAsync();
            await database.PrepareLegacyVersionAsync(version);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    internal async Task<SqlConnection> OpenConnectionAsync(int? clientSchema = null)
    {
        ThrowIfDisposed();
        var connection = new SqlConnection(_databaseConnectionString);
        await connection.OpenAsync();
        if (clientSchema is null)
            return connection;

        await using var context = connection.CreateCommand();
        context.CommandText = "EXEC sys.sp_set_session_context @key = N'WareProClientSchema', @value = @schema;";
        context.Parameters.Add("@schema", SqlDbType.Int).Value = clientSchema.Value;
        await context.ExecuteNonQueryAsync();
        return connection;
    }

    internal Task ApplyBaselineAsync() => ExecuteTransactionAsync(DatabaseSchemaScripts.BaselineBatches);

    internal Task ApplyUpgradeAsync() =>
        ExecuteAsync(DatabaseSchemaScripts.BuildUpgradeSql(6, "1.1.0"));

    internal Task ApplyCanonicalV6Async() => ExecuteAsync(DatabaseSchemaScripts.SchemaVersion6);

    internal Task ApplyFinalizeAsync() =>
        ExecuteTransactionAsync([DatabaseSchemaScripts.BuildFinalizeSql(6, "1.1.0")]);

internal async Task PrepareLegacyVersionAsync(int currentVersion)
    {
        if (currentVersion is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(currentVersion));

        await ExecuteAsync(BuildHistoricalReversalSql(currentVersion));
        var versions = new[]
        {
            DatabaseSchemaScripts.SchemaVersion1, DatabaseSchemaScripts.SchemaVersion2,
            DatabaseSchemaScripts.SchemaVersion3, DatabaseSchemaScripts.SchemaVersion4,
            DatabaseSchemaScripts.SchemaVersion5
        };
        await ExecuteTransactionAsync([DatabaseSchemaScripts.SchemaMetadata, .. versions.Take(currentVersion)]);
        await ExecuteAsync("UPDATE dbo.__WareProSchemaVersion SET Version = @version WHERE Id = 1;", ("@version", currentVersion));
    }

    private static string BuildHistoricalReversalSql(int version) => $$"""
        ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
        IF OBJECT_ID(N'dbo.__WareProClientSession', N'U') IS NOT NULL DROP TABLE dbo.__WareProClientSession;
        DECLARE @t sysname;
        DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT [name] FROM (VALUES
            (N'AppUser'),(N'AuditArchiveManifest'),(N'Brand'),(N'Category'),(N'Customer'),(N'Product'),(N'ProductSerial'),(N'ProductUnit'),
            (N'PurchaseInvoice'),(N'PurchaseInvoiceLine'),(N'SalesInvoice'),(N'SalesInvoiceLine'),(N'StockAdjustment'),(N'StockAdjustmentLine'),
            (N'StockBalance'),(N'StockCountLine'),(N'StockCountSession'),(N'StockIn'),(N'StockInLine'),(N'StockOut'),(N'StockOutLine'),
            (N'StockTransfer'),(N'StockTransferLine'),(N'Supplier'),(N'Unit'),(N'Warehouse'),(N'WarrantyClaim'),(N'WarrantyCoverage')) x([name])
            WHERE COL_LENGTH(N'dbo.' + [name], N'RowVersion') IS NOT NULL;
        DECLARE @drop nvarchar(max); OPEN c; FETCH NEXT FROM c INTO @t; WHILE @@FETCH_STATUS = 0 BEGIN SET @drop = N'ALTER TABLE dbo.' + QUOTENAME(@t) + N' DROP COLUMN RowVersion;'; EXEC sys.sp_executesql @drop; FETCH NEXT FROM c INTO @t; END; CLOSE c; DEALLOCATE c;
        IF {{version}} < 5 BEGIN IF OBJECT_ID(N'dbo.CK_SalesInvoice_PaymentStatus', N'C') IS NOT NULL ALTER TABLE dbo.SalesInvoice DROP CONSTRAINT CK_SalesInvoice_PaymentStatus; IF OBJECT_ID(N'dbo.CK_PurchaseInvoice_PaymentStatus', N'C') IS NOT NULL ALTER TABLE dbo.PurchaseInvoice DROP CONSTRAINT CK_PurchaseInvoice_PaymentStatus; END;
        IF {{version}} < 4 BEGIN
            DECLARE @indexTable sysname, @indexName sysname;
            DECLARE indexes CURSOR LOCAL FAST_FORWARD FOR
                SELECT OBJECT_NAME(i.object_id), i.name
                FROM sys.indexes i
                WHERE i.is_primary_key = 0 AND i.is_unique_constraint = 0
                    AND OBJECT_NAME(i.object_id) IN (N'StockIn', N'StockOut', N'StockAdjustment', N'StockTransfer')
                    AND EXISTS (
                        SELECT 1 FROM sys.index_columns ic
                        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                        WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id
                            AND c.name IN (N'ApprovedAt', N'PostedAt'));
            OPEN indexes; FETCH NEXT FROM indexes INTO @indexTable, @indexName;
            WHILE @@FETCH_STATUS = 0 BEGIN
                SET @drop = N'DROP INDEX ' + QUOTENAME(@indexName) + N' ON dbo.' + QUOTENAME(@indexTable) + N';';
                EXEC sys.sp_executesql @drop;
                FETCH NEXT FROM indexes INTO @indexTable, @indexName;
            END;
            CLOSE indexes; DEALLOCATE indexes;
            ALTER TABLE dbo.StockIn DROP COLUMN ApprovedAt, PostedAt;
            ALTER TABLE dbo.StockOut DROP COLUMN ApprovedAt, PostedAt;
            ALTER TABLE dbo.StockAdjustment DROP COLUMN ApprovedAt, PostedAt;
            ALTER TABLE dbo.StockTransfer DROP COLUMN ApprovedAt, PostedAt;
        END;
        IF {{version}} < 3 AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.WarrantyClaim') AND name=N'IX_WarrantyClaim_ProductSerialId') DROP INDEX IX_WarrantyClaim_ProductSerialId ON dbo.WarrantyClaim;
        IF {{version}} < 2 BEGIN
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ProductSerial') AND name=N'IX_ProductSerial_Product_Warehouse_Status') DROP INDEX IX_ProductSerial_Product_Warehouse_Status ON dbo.ProductSerial;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.PurchaseInvoice') AND name=N'IX_PurchaseInvoice_PaymentStatus_InvoiceDate') DROP INDEX IX_PurchaseInvoice_PaymentStatus_InvoiceDate ON dbo.PurchaseInvoice;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.SalesInvoice') AND name=N'IX_SalesInvoice_PaymentStatus_InvoiceDate') DROP INDEX IX_SalesInvoice_PaymentStatus_InvoiceDate ON dbo.SalesInvoice;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StockIn') AND name=N'IX_StockIn_CreatedAt') DROP INDEX IX_StockIn_CreatedAt ON dbo.StockIn;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StockIn') AND name=N'IX_StockIn_Status_ImportDate') DROP INDEX IX_StockIn_Status_ImportDate ON dbo.StockIn;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StockOut') AND name=N'IX_StockOut_CreatedAt') DROP INDEX IX_StockOut_CreatedAt ON dbo.StockOut;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.StockOut') AND name=N'IX_StockOut_Status_ExportDate') DROP INDEX IX_StockOut_Status_ExportDate ON dbo.StockOut;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.WarrantyClaim') AND name=N'IX_WarrantyClaim_Status') DROP INDEX IX_WarrantyClaim_Status ON dbo.WarrantyClaim;
        END;
        """;
    internal Task ApplyLegacyTransferShapeAsync() => ExecuteAsync("""
        DECLARE @defaultName sysname =
        (
            SELECT dc.name
            FROM sys.default_constraints AS dc
            INNER JOIN sys.columns AS c
                ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.StockTransfer') AND c.name = N'CreatedAt'
        );
        IF @defaultName IS NOT NULL
        BEGIN
            DECLARE @dropDefaultSql nvarchar(max) =
                N'ALTER TABLE dbo.StockTransfer DROP CONSTRAINT ' + QUOTENAME(@defaultName);
            EXEC sys.sp_executesql @dropDefaultSql;
        END;
        ALTER TABLE dbo.StockTransfer ALTER COLUMN TransferDate DATETIME NOT NULL;
        ALTER TABLE dbo.StockTransfer ALTER COLUMN CreatedAt DATETIME NOT NULL;
        ALTER TABLE dbo.StockTransfer ALTER COLUMN Notes NVARCHAR(MAX) NULL;
        IF OBJECT_ID(N'dbo.FK_StockTransfer_FromWarehouse', N'F') IS NOT NULL
            ALTER TABLE dbo.StockTransfer DROP CONSTRAINT FK_StockTransfer_FromWarehouse;
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'UX_StockTransfer_DocumentCode')
            DROP INDEX UX_StockTransfer_DocumentCode ON dbo.StockTransfer;
        """);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        ValidateDatabaseName(DatabaseName);
SqlConnection.ClearAllPools();
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(@name) IS NOT NULL
            BEGIN
                DECLARE @sql nvarchar(max) =
                    N'ALTER DATABASE ' + QUOTENAME(@name) +
                    N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@name) + N';';
                EXEC sys.sp_executesql @sql;
            END;
            """;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = DatabaseName;
        await command.ExecuteNonQueryAsync();
        _disposed = true;
    }

    internal static void ValidateDatabaseName(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName)
            || !databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to use an unguarded database.");
    }

    internal async Task<int?> GetDatabaseIdAsync()
    {
        ValidateDatabaseName(DatabaseName);
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_ID(@name);";
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = DatabaseName;
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : Convert.ToInt32(value);
    }
    private async Task ExecuteTransactionAsync(IReadOnlyList<string> batches)
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            foreach (var sql in batches)
            {
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandTimeout = 300;
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerTestDatabase.ConnectionStringVariable)))
            Skip = $"Set {SqlServerTestDatabase.ConnectionStringVariable} to a disposable SQL Server master database.";
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class SqlServerTheoryAttribute : TheoryAttribute
{
    public SqlServerTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerTestDatabase.ConnectionStringVariable)))
            Skip = $"Set {SqlServerTestDatabase.ConnectionStringVariable} to a disposable SQL Server master database.";
    }
}
