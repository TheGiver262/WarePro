SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS
(
    SELECT StockOutId
    FROM dbo.SalesInvoice
    WHERE StockOutId IS NOT NULL
    GROUP BY StockOutId
    HAVING COUNT_BIG(*) > 1
)
BEGIN
    THROW 51008, 'Schema 8 upgrade blocked: duplicate stock-out links exist in sales invoices.', 1;
END;

IF EXISTS
(
    SELECT StockInId
    FROM dbo.PurchaseInvoice
    WHERE StockInId IS NOT NULL
    GROUP BY StockInId
    HAVING COUNT_BIG(*) > 1
)
BEGIN
    THROW 51009, 'Schema 8 upgrade blocked: duplicate stock-in links exist in purchase invoices.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalesInvoice')
      AND name = N'UX_SalesInvoice_StockOutId'
      AND (is_unique = 0 OR has_filter = 0)
)
BEGIN
    DROP INDEX UX_SalesInvoice_StockOutId ON dbo.SalesInvoice;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalesInvoice')
      AND name = N'UX_SalesInvoice_StockOutId'
)
BEGIN
    EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_SalesInvoice_StockOutId
        ON dbo.SalesInvoice(StockOutId)
        WHERE StockOutId IS NOT NULL;';
END;

IF EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PurchaseInvoice')
      AND name = N'UX_PurchaseInvoice_StockInId'
      AND (is_unique = 0 OR has_filter = 0)
)
BEGIN
    DROP INDEX UX_PurchaseInvoice_StockInId ON dbo.PurchaseInvoice;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PurchaseInvoice')
      AND name = N'UX_PurchaseInvoice_StockInId'
)
BEGIN
    EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_PurchaseInvoice_StockInId
        ON dbo.PurchaseInvoice(StockInId)
        WHERE StockInId IS NOT NULL;';
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.WarrantyClaim AS claim
    INNER JOIN dbo.WarrantyCoverage AS coverage
        ON coverage.Id = claim.WarrantyCoverageId
    WHERE coverage.ProductSerialId <> claim.ProductSerialId
)
BEGIN
    THROW 51010, 'Schema 8 upgrade blocked: mismatched warranty coverage serials exist.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.WarrantyCoverage')
      AND name = N'AK_WarrantyCoverage_Id_ProductSerialId'
)
BEGIN
    ALTER TABLE dbo.WarrantyCoverage
        ADD CONSTRAINT AK_WarrantyCoverage_Id_ProductSerialId
            UNIQUE (Id, ProductSerialId);
END;

IF OBJECT_ID(N'dbo.FK_WarrantyClaim_Coverage', N'F') IS NOT NULL
BEGIN
    ALTER TABLE dbo.WarrantyClaim
        DROP CONSTRAINT FK_WarrantyClaim_Coverage;
END;

ALTER TABLE dbo.WarrantyClaim WITH CHECK
    ADD CONSTRAINT FK_WarrantyClaim_Coverage
        FOREIGN KEY (WarrantyCoverageId, ProductSerialId)
        REFERENCES dbo.WarrantyCoverage(Id, ProductSerialId);

ALTER TABLE dbo.WarrantyClaim
    CHECK CONSTRAINT FK_WarrantyClaim_Coverage;

IF OBJECT_ID(N'dbo.__WareProSchemaVersion', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.__WareProSchemaVersion
    SET [Version] = 8,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Id = 1 AND [Version] < 8;
END;
