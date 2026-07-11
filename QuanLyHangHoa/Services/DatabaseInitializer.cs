using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services.DataImport;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Services;

public sealed class DatabaseInitializer
{
    private const int CurrentSchemaVersion = 3;

    private const string SchemaVersion1Sql = """
        IF COL_LENGTH('Product', 'Description') IS NULL ALTER TABLE Product ADD Description NVARCHAR(MAX);
        IF COL_LENGTH('Product', 'CostPrice') IS NULL ALTER TABLE Product ADD CostPrice DECIMAL(18,2);
        IF COL_LENGTH('ProductSerial', 'Note') IS NULL ALTER TABLE ProductSerial ADD Note NVARCHAR(MAX);
        IF COL_LENGTH('ProductSerial', 'StockTransferLineId') IS NULL ALTER TABLE ProductSerial ADD StockTransferLineId INT;
        IF COL_LENGTH('SalesInvoice', 'CreatedAt') IS NULL ALTER TABLE SalesInvoice ADD CreatedAt DATETIME;
        IF COL_LENGTH('SalesInvoice', 'Notes') IS NULL ALTER TABLE SalesInvoice ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('SalesInvoice', 'PaidAmount') IS NULL ALTER TABLE SalesInvoice ADD PaidAmount DECIMAL(18,2);
        IF COL_LENGTH('SalesInvoice', 'PaymentStatus') IS NULL ALTER TABLE SalesInvoice ADD PaymentStatus NVARCHAR(50);
        IF COL_LENGTH('SalesInvoice', 'DueDate') IS NULL ALTER TABLE SalesInvoice ADD DueDate DATETIME;
        IF COL_LENGTH('PurchaseInvoice', 'CreatedAt') IS NULL ALTER TABLE PurchaseInvoice ADD CreatedAt DATETIME;
        IF COL_LENGTH('PurchaseInvoice', 'Notes') IS NULL ALTER TABLE PurchaseInvoice ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('PurchaseInvoice', 'PaidAmount') IS NULL ALTER TABLE PurchaseInvoice ADD PaidAmount DECIMAL(18,2);
        IF COL_LENGTH('PurchaseInvoice', 'PaymentStatus') IS NULL ALTER TABLE PurchaseInvoice ADD PaymentStatus NVARCHAR(50);
        IF COL_LENGTH('PurchaseInvoice', 'DueDate') IS NULL ALTER TABLE PurchaseInvoice ADD DueDate DATETIME;
        IF COL_LENGTH('StockIn', 'ImportDate') IS NULL ALTER TABLE StockIn ADD ImportDate DATETIME;
        IF COL_LENGTH('StockIn', 'Notes') IS NULL ALTER TABLE StockIn ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockIn', 'UpdatedAt') IS NULL ALTER TABLE StockIn ADD UpdatedAt DATETIME;
        IF COL_LENGTH('StockIn', 'UpdatedBy') IS NULL ALTER TABLE StockIn ADD UpdatedBy INT;
        IF COL_LENGTH('StockOut', 'ExportDate') IS NULL ALTER TABLE StockOut ADD ExportDate DATETIME;
        IF COL_LENGTH('StockOut', 'Notes') IS NULL ALTER TABLE StockOut ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockOut', 'UpdatedAt') IS NULL ALTER TABLE StockOut ADD UpdatedAt DATETIME;
        IF COL_LENGTH('StockOut', 'UpdatedBy') IS NULL ALTER TABLE StockOut ADD UpdatedBy INT;
        IF COL_LENGTH('StockOutLine', 'DraftSerials') IS NULL ALTER TABLE StockOutLine ADD DraftSerials NVARCHAR(MAX);
        IF COL_LENGTH('StockInLine', 'DraftSerials') IS NULL ALTER TABLE StockInLine ADD DraftSerials NVARCHAR(MAX);
        IF COL_LENGTH('StockAdjustment', 'Notes') IS NULL ALTER TABLE StockAdjustment ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockCountSession', 'Notes') IS NULL ALTER TABLE StockCountSession ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockTransfer', 'Notes') IS NULL ALTER TABLE StockTransfer ADD Notes NVARCHAR(MAX);
        IF COL_LENGTH('StockTransfer', 'UpdatedAt') IS NULL ALTER TABLE StockTransfer ADD UpdatedAt DATETIME;
        IF COL_LENGTH('StockTransfer', 'UpdatedBy') IS NULL ALTER TABLE StockTransfer ADD UpdatedBy INT;
        """;

    private const string SchemaVersion2Sql = """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductSerial_Product_Warehouse_Status' AND object_id = OBJECT_ID('ProductSerial'))
            CREATE INDEX IX_ProductSerial_Product_Warehouse_Status ON ProductSerial (ProductId, CurrentWarehouseId, CurrentStatus);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PurchaseInvoice_PaymentStatus_InvoiceDate' AND object_id = OBJECT_ID('PurchaseInvoice'))
            CREATE INDEX IX_PurchaseInvoice_PaymentStatus_InvoiceDate ON PurchaseInvoice (PaymentStatus, InvoiceDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SalesInvoice_PaymentStatus_InvoiceDate' AND object_id = OBJECT_ID('SalesInvoice'))
            CREATE INDEX IX_SalesInvoice_PaymentStatus_InvoiceDate ON SalesInvoice (PaymentStatus, InvoiceDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockIn_CreatedAt' AND object_id = OBJECT_ID('StockIn'))
            CREATE INDEX IX_StockIn_CreatedAt ON StockIn (CreatedAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockIn_Status_ImportDate' AND object_id = OBJECT_ID('StockIn'))
            CREATE INDEX IX_StockIn_Status_ImportDate ON StockIn (Status, ImportDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockOut_CreatedAt' AND object_id = OBJECT_ID('StockOut'))
            CREATE INDEX IX_StockOut_CreatedAt ON StockOut (CreatedAt);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StockOut_Status_ExportDate' AND object_id = OBJECT_ID('StockOut'))
            CREATE INDEX IX_StockOut_Status_ExportDate ON StockOut (Status, ExportDate);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WarrantyClaim_Status' AND object_id = OBJECT_ID('WarrantyClaim'))
            CREATE INDEX IX_WarrantyClaim_Status ON WarrantyClaim (Status);
        """;

    private const string SchemaVersion3Sql = """
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_WarrantyClaim_OpenClaim_PerSerial' AND object_id = OBJECT_ID('WarrantyClaim'))
            DROP INDEX UX_WarrantyClaim_OpenClaim_PerSerial ON WarrantyClaim;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WarrantyClaim_ProductSerialId' AND object_id = OBJECT_ID('WarrantyClaim'))
            CREATE INDEX IX_WarrantyClaim_ProductSerialId ON WarrantyClaim (ProductSerialId);
        """;
    private readonly Func<AppDbContext> _contextFactory;
    private readonly string _baseDirectory;

    public DatabaseInitializer(Func<AppDbContext> contextFactory, string baseDirectory)
    {
        _contextFactory = contextFactory;
        _baseDirectory = baseDirectory;
    }

    public void Initialize()
    {
        var stopwatch = Stopwatch.StartNew();
        var forceSeed = StartupSeedPolicy.IsForceSeedEnabled();

        if (TryGetDatabaseState(out var schemaVersion, out var hasAnyUsers)
            && StartupSeedPolicy.CanSkipInitialization(
                schemaVersion,
                CurrentSchemaVersion,
                hasAnyUsers,
                forceSeed))
        {
            Trace.WriteLine($"[STARTUP] Database fast path: {stopwatch.ElapsedMilliseconds} ms");
            return;
        }

        using var db = _contextFactory();
        db.Database.EnsureCreated();
        Trace.WriteLine($"[STARTUP] EnsureCreated: {stopwatch.ElapsedMilliseconds} ms");
        ApplySchemaUpdates(db);
        Trace.WriteLine($"[STARTUP] Schema ready: {stopwatch.ElapsedMilliseconds} ms");
        SeedIfNeeded(db);
        Trace.WriteLine($"[STARTUP] Seed check complete: {stopwatch.ElapsedMilliseconds} ms");
    }

    private static bool TryGetDatabaseState(out int schemaVersion, out bool hasAnyUsers)
    {
        schemaVersion = 0;
        hasAnyUsers = false;

        try
        {
            using var connection = new SqlConnection(AppDbContext.GetConnectionString());
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
                   OR OBJECT_ID(N'[dbo].[AppUser]', N'U') IS NULL
                    SELECT 0 AS [SchemaVersion], CAST(0 AS BIT) AS [HasAnyUsers];
                ELSE
                    SELECT
                        ISNULL((SELECT MAX([Version]) FROM [dbo].[__WareProSchemaVersion]), 0),
                        CAST(CASE WHEN EXISTS (SELECT TOP (1) 1 FROM [dbo].[AppUser]) THEN 1 ELSE 0 END AS BIT);
                """;

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return false;
            }

            schemaVersion = reader.GetInt32(0);
            hasAnyUsers = reader.GetBoolean(1);
            return true;
        }
        catch (SqlException)
        {
            return false;
        }
    }

    private static void ApplySchemaUpdates(AppDbContext db)
    {
        if (GetCurrentSchemaVersion(db) >= CurrentSchemaVersion)
        {
            return;
        }

        var sql = $$"""
            IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[__WareProSchemaVersion]
                (
                    [Id] INT NOT NULL CONSTRAINT [PK___WareProSchemaVersion] PRIMARY KEY,
                    [Version] INT NOT NULL,
                    [UpdatedAt] DATETIME2 NOT NULL
                );
                INSERT INTO [dbo].[__WareProSchemaVersion] ([Id], [Version], [UpdatedAt])
                VALUES (1, 0, SYSUTCDATETIME());
            END;

            DECLARE @CurrentVersion INT = ISNULL(
                (SELECT TOP (1) [Version] FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1), 0);

            IF @CurrentVersion < 1
            BEGIN
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransfer')
                    CREATE TABLE StockTransfer (Id INT IDENTITY(1,1) PRIMARY KEY, DocumentCode NVARCHAR(50) NOT NULL, FromWarehouseId INT NOT NULL, ToWarehouseId INT NOT NULL, Status NVARCHAR(50) NOT NULL, TransferDate DATETIME NOT NULL, Notes NVARCHAR(500), CreatedBy INT NOT NULL, ApprovedBy INT, PostedBy INT, CreatedAt DATETIME DEFAULT GETUTCDATE(), UpdatedAt DATETIME, UpdatedBy INT);
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockTransferLine')
                    CREATE TABLE StockTransferLine (Id INT IDENTITY(1,1) PRIMARY KEY, StockTransferId INT NOT NULL, ProductId INT NOT NULL, UnitId INT NOT NULL, Quantity DECIMAL(18,2) NOT NULL, BaseQuantity DECIMAL(18,2) NOT NULL);

                {{SchemaVersion1Sql}}
            END;

            IF @CurrentVersion < 2
            BEGIN
                {{SchemaVersion2Sql}}
            END;

            IF @CurrentVersion < 3
            BEGIN
                {{SchemaVersion3Sql}}
            END;
            UPDATE [dbo].[__WareProSchemaVersion]
            SET [Version] = {{CurrentSchemaVersion}}, [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Id] = 1 AND [Version] < {{CurrentSchemaVersion}};
            """;

        db.Database.ExecuteSqlRaw(sql);
    }

    private static int GetCurrentSchemaVersion(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        try
        {
            if (shouldClose)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = """
                IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
                    SELECT 0;
                ELSE
                    EXEC sp_executesql N'SELECT ISNULL(MAX([Version]), 0) FROM [dbo].[__WareProSchemaVersion];';
                """;

            return Convert.ToInt32(command.ExecuteScalar());
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private void SeedIfNeeded(AppDbContext db)
    {
        var excelPath = Path.Combine(_baseDirectory, "Database", "warepro_database_seed.xlsx");
        if (!File.Exists(excelPath))
        {
            return;
        }

        var forceSeed = StartupSeedPolicy.IsForceSeedEnabled();
        var hasAnyUsers = !forceSeed && db.AppUsers.AsNoTracking().Any();
        if (!StartupSeedPolicy.ShouldSeed(seedFileExists: true, hasAnyUsers, forceSeed))
        {
            return;
        }

        try
        {
            var seeder = new DatabaseSeeder(db, excelPath);
            var log = Task.Run(seeder.SeedAsync).GetAwaiter().GetResult();
            Console.WriteLine($"[SEED] Result: {log}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Seed Error: {ex.Message}");
        }
    }
}
