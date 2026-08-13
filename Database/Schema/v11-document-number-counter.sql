IF OBJECT_ID(N'dbo.DocumentNumberCounter', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentNumberCounter
    (
        DocumentType NVARCHAR(32) NOT NULL,
        BusinessDate DATE NOT NULL,
        LastValue BIGINT NOT NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT PK_DocumentNumberCounter PRIMARY KEY (DocumentType, BusinessDate),
        CONSTRAINT CK_DocumentNumberCounter_LastValue CHECK (LastValue > 0)
    );
END;

INSERT dbo.ProductUnit
    (ProductId, UnitId, ConversionFactor, IsBaseUnit, IsPurchaseUnit, IsSalesUnit)
SELECT
    product.Id,
    product.DefaultUnitId,
    CAST(1 AS DECIMAL(18, 6)),
    CASE WHEN EXISTS
    (
        SELECT 1
        FROM dbo.ProductUnit AS existingBase
        WHERE existingBase.ProductId = product.Id
          AND existingBase.IsBaseUnit = 1
    ) THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END,
    CAST(1 AS BIT),
    CAST(1 AS BIT)
FROM dbo.Product AS product
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.ProductUnit AS existingMapping
    WHERE existingMapping.ProductId = product.Id
      AND existingMapping.UnitId = product.DefaultUnitId
);

EXEC sys.sp_executesql N'
CREATE OR ALTER PROCEDURE dbo.AllocateDocumentNumber
    @DocumentType NVARCHAR(32),
    @BusinessDate DATE
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @StartedTransaction BIT = 0;
    IF @@TRANCOUNT = 0
    BEGIN
        BEGIN TRANSACTION;
        SET @StartedTransaction = 1;
    END;

    BEGIN TRY
        DECLARE @Allocated TABLE ([Value] BIGINT NOT NULL);
        UPDATE dbo.DocumentNumberCounter WITH (UPDLOCK, HOLDLOCK)
        SET LastValue = LastValue + 1
        OUTPUT inserted.LastValue INTO @Allocated ([Value])
        WHERE DocumentType = @DocumentType
          AND BusinessDate = @BusinessDate;

        IF NOT EXISTS (SELECT 1 FROM @Allocated)
        BEGIN
            INSERT dbo.DocumentNumberCounter (DocumentType, BusinessDate, LastValue)
            OUTPUT inserted.LastValue INTO @Allocated ([Value])
            VALUES (@DocumentType, @BusinessDate, 1);
        END;

        SELECT TOP (1) [Value] FROM @Allocated;
        IF @StartedTransaction = 1 COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @StartedTransaction = 1 AND XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;';
