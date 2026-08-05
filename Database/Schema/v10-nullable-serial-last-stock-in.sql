IF OBJECT_ID(N'dbo.ProductSerial', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ProductSerial', N'LastStockInLineId') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.ProductSerial')
          AND name = N'LastStockInLineId'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE dbo.ProductSerial ALTER COLUMN LastStockInLineId INT NULL;
    END;

    -- Collision preflight guard before uppercase normalization
    IF EXISTS
    (
        SELECT UPPER(TRIM(SerialNumber))
        FROM dbo.ProductSerial
        GROUP BY UPPER(TRIM(SerialNumber))
        HAVING COUNT_BIG(*) > 1
    )
    BEGIN
        THROW 51008, 'Schema 10 upgrade blocked: duplicate serial numbers exist when normalized to uppercase.', 1;
    END;

    -- Canonicalize serial numbers to UPPER(TRIM(SerialNumber)) for invariant lookup
    UPDATE dbo.ProductSerial
    SET SerialNumber = UPPER(TRIM(SerialNumber))
    WHERE SerialNumber <> UPPER(TRIM(SerialNumber));
END;
