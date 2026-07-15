SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @Source TABLE
    (
        ProductCode NVARCHAR(50) NOT NULL,
        UnitCode NVARCHAR(50) NOT NULL,
        ConversionFactor DECIMAL(18,6) NOT NULL,
        IsBaseUnit BIT NOT NULL,
        IsPurchaseUnit BIT NOT NULL,
        IsSalesUnit BIT NOT NULL,
        PRIMARY KEY (ProductCode, UnitCode)
    );

    INSERT INTO @Source
        (ProductCode, UnitCode, ConversionFactor, IsBaseUnit, IsPurchaseUnit, IsSalesUnit)
    VALUES
        (N'PRD0001', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0002', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0003', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0004', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0005', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0006', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0007', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0008', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0009', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0010', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0010', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0011', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0012', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0012', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0013', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0014', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0015', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0016', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0017', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0018', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0019', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0020', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0021', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0022', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0023', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0024', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0024', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0024', N'UNIT002', 2, 0, 1, 1),
        (N'PRD0025', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0026', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0026', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0027', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0027', N'UNIT002', 2, 0, 1, 1),
        (N'PRD0028', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0029', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0030', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0031', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0032', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0033', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0034', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0035', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0036', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0037', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0038', N'UNIT006', 1, 1, 1, 1),
        (N'PRD0038', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0039', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0040', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0041', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0042', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0042', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0043', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0044', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0045', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0046', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0047', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0048', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0048', N'UNIT003', 10, 0, 1, 0),
        (N'PRD0049', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0050', N'UNIT001', 1, 1, 1, 1),
        (N'PRD0050', N'UNIT003', 10, 0, 1, 0);

    IF (SELECT COUNT(*) FROM @Source) <> 60
        THROW 51000, 'Expected 60 ProductUnit source rows.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @Source AS source
        LEFT JOIN dbo.Product AS product ON product.ProductCode = source.ProductCode
        LEFT JOIN dbo.Unit AS unit ON unit.UnitCode = source.UnitCode
        WHERE product.Id IS NULL OR unit.Id IS NULL
    )
        THROW 51001, 'Unresolved ProductUnit source reference.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @Source AS source
        JOIN dbo.Product AS product ON product.ProductCode = source.ProductCode
        JOIN dbo.Unit AS unit ON unit.UnitCode = source.UnitCode
        JOIN dbo.ProductUnit AS existing
            ON existing.ProductId = product.Id
           AND existing.IsBaseUnit = 1
           AND existing.UnitId <> unit.Id
        WHERE source.IsBaseUnit = 1
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.ProductUnit AS samePair
              WHERE samePair.ProductId = product.Id
                AND samePair.UnitId = unit.Id
          )
    )
        THROW 51002, 'A product already has a different base unit.', 1;

    INSERT INTO dbo.ProductUnit
        (ProductId, UnitId, ConversionFactor, IsBaseUnit, IsPurchaseUnit, IsSalesUnit)
    SELECT
        product.Id,
        unit.Id,
        source.ConversionFactor,
        source.IsBaseUnit,
        source.IsPurchaseUnit,
        source.IsSalesUnit
    FROM @Source AS source
    JOIN dbo.Product AS product ON product.ProductCode = source.ProductCode
    JOIN dbo.Unit AS unit ON unit.UnitCode = source.UnitCode
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.ProductUnit AS existing
        WHERE existing.ProductId = product.Id
          AND existing.UnitId = unit.Id
    );

    DECLARE @InsertedRows INT = @@ROWCOUNT;
    DECLARE @ResolvedRows INT =
    (
        SELECT COUNT(*)
        FROM @Source AS source
        JOIN dbo.Product AS product ON product.ProductCode = source.ProductCode
        JOIN dbo.Unit AS unit ON unit.UnitCode = source.UnitCode
        JOIN dbo.ProductUnit AS productUnit
            ON productUnit.ProductId = product.Id
           AND productUnit.UnitId = unit.Id
    );

    IF @ResolvedRows <> 60
        THROW 51003, 'Not all ProductUnit source rows exist after backfill.', 1;

    IF N'$(DryRun)' = N'1'
        ROLLBACK TRANSACTION;
    ELSE
        COMMIT TRANSACTION;

    SELECT @InsertedRows AS InsertedRows, @ResolvedRows AS ResolvedRows, N'$(DryRun)' AS DryRun;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
