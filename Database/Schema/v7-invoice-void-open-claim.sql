SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.PurchaseInvoice', N'Status') IS NULL
BEGIN
    ALTER TABLE dbo.PurchaseInvoice
        ADD [Status] NVARCHAR(20) NOT NULL
            CONSTRAINT DF_PurchaseInvoice_Status DEFAULT (N'Active');
END;

IF OBJECT_ID(N'dbo.CK_PurchaseInvoice_Status', N'C') IS NULL
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.PurchaseInvoice WITH CHECK
            ADD CONSTRAINT CK_PurchaseInvoice_Status CHECK ([Status] IN (N''Active'', N''Voided''));
        ALTER TABLE dbo.PurchaseInvoice CHECK CONSTRAINT CK_PurchaseInvoice_Status;';
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PurchaseInvoice')
      AND name = N'IX_PurchaseInvoice_Status_InvoiceDate'
)
BEGIN
    EXEC sys.sp_executesql N'CREATE INDEX IX_PurchaseInvoice_Status_InvoiceDate
        ON dbo.PurchaseInvoice([Status], InvoiceDate);';
END;

IF COL_LENGTH(N'dbo.SalesInvoice', N'Status') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoice
        ADD [Status] NVARCHAR(20) NOT NULL
            CONSTRAINT DF_SalesInvoice_Status DEFAULT (N'Active');
END;

IF OBJECT_ID(N'dbo.CK_SalesInvoice_Status', N'C') IS NULL
BEGIN
    EXEC sys.sp_executesql N'
        ALTER TABLE dbo.SalesInvoice WITH CHECK
            ADD CONSTRAINT CK_SalesInvoice_Status CHECK ([Status] IN (N''Active'', N''Voided''));
        ALTER TABLE dbo.SalesInvoice CHECK CONSTRAINT CK_SalesInvoice_Status;';
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalesInvoice')
      AND name = N'IX_SalesInvoice_Status_InvoiceDate'
)
BEGIN
    EXEC sys.sp_executesql N'CREATE INDEX IX_SalesInvoice_Status_InvoiceDate
        ON dbo.SalesInvoice([Status], InvoiceDate);';
END;

IF EXISTS
(
    SELECT ProductSerialId
    FROM dbo.WarrantyClaim
    WHERE [Status] NOT IN (N'Closed', N'Rejected')
    GROUP BY ProductSerialId
    HAVING COUNT_BIG(*) > 1
)
BEGIN
    THROW 51007, 'Schema 7 upgrade blocked: duplicate open warranty claims exist for a serial.', 1;
END;

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WarrantyClaim')
      AND name = N'UX_WarrantyClaim_OpenClaim_PerSerial'
)
BEGIN
    DROP INDEX UX_WarrantyClaim_OpenClaim_PerSerial ON dbo.WarrantyClaim;
END;

IF COL_LENGTH(N'dbo.WarrantyClaim', N'OpenProductSerialId') IS NULL
BEGIN
    ALTER TABLE dbo.WarrantyClaim
        ADD OpenProductSerialId AS
            (CASE
                WHEN [Status] IN (N'Closed', N'Rejected') THEN NULL
                ELSE ProductSerialId
             END) PERSISTED;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.WarrantyClaim')
      AND name = N'UX_WarrantyClaim_OpenProductSerialId'
)
BEGIN
    EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_WarrantyClaim_OpenProductSerialId
        ON dbo.WarrantyClaim(ProductSerialId)
        WHERE [Status] <> N''Closed'' AND [Status] <> N''Rejected'';';
END;

IF OBJECT_ID(N'dbo.__WareProSchemaVersion', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.__WareProSchemaVersion
    SET [Version] = 7,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Id = 1 AND [Version] < 7;
END;
