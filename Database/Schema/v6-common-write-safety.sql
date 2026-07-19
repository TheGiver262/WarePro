SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ALTER DATABASE must run outside a user transaction.
IF EXISTS
(
    SELECT 1
    FROM sys.databases
    WHERE [name] = DB_NAME()
      AND [is_read_committed_snapshot_on] = 0
)
BEGIN
    DECLARE @EnableRcsiSql NVARCHAR(MAX) =
        N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) +
        N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;';
    EXEC sys.sp_executesql @EnableRcsiSql;
END;

IF OBJECT_ID(N'[dbo].[AppUser]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AppUser', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[AppUser] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[AuditArchiveManifest]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AuditArchiveManifest', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[AuditArchiveManifest] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Brand]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Brand', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Brand] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Category]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Category', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Category] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Customer]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Customer', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Customer] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Product]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Product', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Product] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[ProductSerial]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ProductSerial', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[ProductSerial] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[ProductUnit]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ProductUnit', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[ProductUnit] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[PurchaseInvoice]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PurchaseInvoice', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[PurchaseInvoice] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[PurchaseInvoiceLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.PurchaseInvoiceLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[PurchaseInvoiceLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[SalesInvoice]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesInvoice', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[SalesInvoice] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[SalesInvoiceLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesInvoiceLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[SalesInvoiceLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockAdjustment]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockAdjustment', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockAdjustment] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockAdjustmentLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockAdjustmentLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockAdjustmentLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockBalance]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockBalance', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockBalance] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockCountLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockCountLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockCountLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockCountSession]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockCountSession', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockCountSession] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockIn]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockIn', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockIn] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockInLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockInLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockInLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockOut]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockOut', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockOut] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockOutLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockOutLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockOutLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockTransfer]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockTransfer', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockTransfer] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[StockTransferLine]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.StockTransferLine', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[StockTransferLine] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Supplier]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Supplier', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Supplier] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Unit]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Unit', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Unit] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[Warehouse]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Warehouse', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[Warehouse] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[WarrantyClaim]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WarrantyClaim', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[WarrantyClaim] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[WarrantyCoverage]', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.WarrantyCoverage', N'RowVersion') IS NULL
    ALTER TABLE [dbo].[WarrantyCoverage] ADD [RowVersion] ROWVERSION NOT NULL;

IF OBJECT_ID(N'[dbo].[__WareProClientSession]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__WareProClientSession]
    (
        [SessionId] UNIQUEIDENTIFIER NOT NULL
            CONSTRAINT [PK___WareProClientSession] PRIMARY KEY,
        [MachineName] NVARCHAR(255) NOT NULL,
        [ProcessId] INT NOT NULL,
        [AppVersion] NVARCHAR(32) NOT NULL,
        [StartedAtUtc] DATETIME2(0) NOT NULL,
        [LastSeenUtc] DATETIME2(0) NOT NULL,
        [RowVersion] ROWVERSION NOT NULL
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID(N'[dbo].[__WareProClientSession]')
      AND [name] = N'IX___WareProClientSession_LastSeenUtc'
)
    CREATE INDEX [IX___WareProClientSession_LastSeenUtc]
        ON [dbo].[__WareProClientSession] ([LastSeenUtc]);

DECLARE @MutableTables TABLE ([TableName] SYSNAME NOT NULL PRIMARY KEY);
INSERT INTO @MutableTables ([TableName])
VALUES
    (N'AppUser'),
    (N'AuditArchiveManifest'),
    (N'Brand'),
    (N'Category'),
    (N'Customer'),
    (N'Product'),
    (N'ProductSerial'),
    (N'ProductUnit'),
    (N'PurchaseInvoice'),
    (N'PurchaseInvoiceLine'),
    (N'SalesInvoice'),
    (N'SalesInvoiceLine'),
    (N'StockAdjustment'),
    (N'StockAdjustmentLine'),
    (N'StockBalance'),
    (N'StockCountLine'),
    (N'StockCountSession'),
    (N'StockIn'),
    (N'StockInLine'),
    (N'StockOut'),
    (N'StockOutLine'),
    (N'StockTransfer'),
    (N'StockTransferLine'),
    (N'Supplier'),
    (N'Unit'),
    (N'Warehouse'),
    (N'WarrantyClaim'),
    (N'WarrantyCoverage');

IF EXISTS
(
    SELECT 1
    FROM @MutableTables
    WHERE OBJECT_ID(N'[dbo].' + QUOTENAME([TableName]), N'U') IS NULL
       OR COL_LENGTH(N'dbo.' + [TableName], N'RowVersion') IS NULL
)
    THROW 51006, 'Schema 6 validation failed: a mutable table or rowversion column is missing.', 1;

IF OBJECT_ID(N'[dbo].[__WareProClientSession]', N'U') IS NULL
    THROW 51006, 'Schema 6 validation failed: the client session table is missing.', 1;

IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[__WareProSchemaVersion]
    (
        [Id] INT NOT NULL CONSTRAINT [PK___WareProSchemaVersion] PRIMARY KEY,
        [Version] INT NOT NULL,
        [MinimumClientVersion] NVARCHAR(32) NOT NULL,
        [AppliedByAppVersion] NVARCHAR(64) NOT NULL,
        [UpdatedAt] DATETIME2 NOT NULL
    );
END;

IF EXISTS (SELECT 1 FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1)
BEGIN
    UPDATE [dbo].[__WareProSchemaVersion]
    SET [Version] = 6,
        [MinimumClientVersion] = N'1.1.0',
        [AppliedByAppVersion] = N'1.1.0',
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [Id] = 1;
END;
ELSE
BEGIN
    INSERT INTO [dbo].[__WareProSchemaVersion]
        ([Id], [Version], [MinimumClientVersion], [AppliedByAppVersion], [UpdatedAt])
    VALUES
        (1, 6, N'1.1.0', N'1.1.0', SYSUTCDATETIME());
END;

