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

    -- Canonicalize serial numbers to UPPER(TRIM(SerialNumber)) for invariant lookup
    UPDATE dbo.ProductSerial
    SET SerialNumber = UPPER(TRIM(SerialNumber))
    WHERE SerialNumber <> UPPER(TRIM(SerialNumber));
END;
