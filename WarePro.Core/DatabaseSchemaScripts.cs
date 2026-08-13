using System.Reflection;

namespace WarePro.Database;

public static class DatabaseSchemaScripts
{
    // migration bundle được tách section một lần; các lần build sau dùng lại cache.
    private static readonly Lazy<IReadOnlyDictionary<string, string>> Sections = new(ReadSections);

    private const string LegacyShapeRepairSql = """
        IF OBJECT_ID(N'dbo.__WareProDatabaseIdentity', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.__WareProDatabaseIdentity
            (
                Id int NOT NULL CONSTRAINT PK___WareProDatabaseIdentity PRIMARY KEY,
                ProductId uniqueidentifier NOT NULL,
                ProductName nvarchar(32) NOT NULL
            );
            INSERT dbo.__WareProDatabaseIdentity (Id, ProductId, ProductName)
            VALUES (1, 'F65EAB95-A3F8-4D8D-9AF5-4839FCA38E21', N'WarePro');
        END;
        IF OBJECT_ID(N'dbo.AuditArchiveManifest', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.AuditArchiveManifest
            (
                Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditArchiveManifest PRIMARY KEY,
                OperationId UNIQUEIDENTIFIER NOT NULL,
                ActorId INT NOT NULL,
                RangeStartUtc DATETIME2(0) NOT NULL,
                RangeEndUtc DATETIME2(0) NOT NULL,
                [RowCount] INT NOT NULL,
                FileName NVARCHAR(260) NOT NULL,
                Sha256Hash NCHAR(64) NOT NULL,
                CreatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_AuditArchiveManifest_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                RowVersion ROWVERSION NOT NULL,
                CONSTRAINT FK_AuditArchiveManifest_Actor FOREIGN KEY (ActorId) REFERENCES dbo.AppUser(Id)
            );
        END;

        IF OBJECT_ID(N'dbo.StockTransfer', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.StockTransfer
            (
                Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockTransfer PRIMARY KEY,
                DocumentCode NVARCHAR(50) NOT NULL,
                FromWarehouseId INT NOT NULL,
                ToWarehouseId INT NOT NULL,
                Status NVARCHAR(50) NOT NULL,
                TransferDate DATETIME2(0) NOT NULL,
                Notes NVARCHAR(500) NULL,
                CreatedBy INT NOT NULL,
                ApprovedBy INT NULL,
                PostedBy INT NULL,
                CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_StockTransfer_CreatedAt DEFAULT (SYSUTCDATETIME()),
                ApprovedAt DATETIME2(0) NULL,
                PostedAt DATETIME2(0) NULL,
                UpdatedAt DATETIME2(0) NULL,
                UpdatedBy INT NULL,
                RowVersion ROWVERSION NOT NULL
            );
        END;

        IF OBJECT_ID(N'dbo.StockTransferLine', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.StockTransferLine
            (
                Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockTransferLine PRIMARY KEY,
                StockTransferId INT NOT NULL,
                ProductId INT NOT NULL,
                UnitId INT NOT NULL,
                Quantity DECIMAL(18,2) NOT NULL,
                BaseQuantity DECIMAL(18,2) NOT NULL,
                RowVersion ROWVERSION NOT NULL
            );
        END;

        IF OBJECT_ID(N'dbo.StockTransfer', N'U') IS NOT NULL
        BEGIN
            DECLARE @StockTransferCreatedAtDefault sysname;
            SELECT @StockTransferCreatedAtDefault = default_constraints.name
            FROM sys.default_constraints AS default_constraints
            WHERE default_constraints.parent_object_id = OBJECT_ID(N'dbo.StockTransfer')
              AND default_constraints.parent_column_id =
                  COLUMNPROPERTY(OBJECT_ID(N'dbo.StockTransfer'), N'CreatedAt', 'ColumnId');
            IF @StockTransferCreatedAtDefault IS NOT NULL
            BEGIN
                DECLARE @DropStockTransferCreatedAtDefault nvarchar(max) =
                    N'ALTER TABLE dbo.StockTransfer DROP CONSTRAINT ' + QUOTENAME(@StockTransferCreatedAtDefault) + N';';
                EXEC sys.sp_executesql @DropStockTransferCreatedAtDefault;
            END;

            ALTER TABLE dbo.StockTransfer ALTER COLUMN TransferDate DATETIME2(0) NOT NULL;
            ALTER TABLE dbo.StockTransfer ALTER COLUMN CreatedAt DATETIME2(0) NOT NULL;
            ALTER TABLE dbo.StockTransfer ALTER COLUMN UpdatedAt DATETIME2(0) NULL;
            ALTER TABLE dbo.StockTransfer ALTER COLUMN Notes NVARCHAR(500) NULL;
            IF NOT EXISTS
            (
                SELECT 1 FROM sys.default_constraints
                WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransfer')
                  AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.StockTransfer'), N'CreatedAt', 'ColumnId')
            )
                ALTER TABLE dbo.StockTransfer ADD CONSTRAINT DF_StockTransfer_CreatedAt
                    DEFAULT (SYSUTCDATETIME()) FOR CreatedAt;
        END;
        IF COL_LENGTH(N'dbo.AuditArchiveManifest', N'RowVersion') IS NULL
            ALTER TABLE dbo.AuditArchiveManifest ADD RowVersion ROWVERSION NOT NULL;
        IF COL_LENGTH(N'dbo.StockTransfer', N'RowVersion') IS NULL
            ALTER TABLE dbo.StockTransfer ADD RowVersion ROWVERSION NOT NULL;
        IF COL_LENGTH(N'dbo.StockTransferLine', N'RowVersion') IS NULL
            ALTER TABLE dbo.StockTransferLine ADD RowVersion ROWVERSION NOT NULL;

        IF COL_LENGTH(N'dbo.PurchaseInvoice', N'CreatedBy') IS NULL
            ALTER TABLE dbo.PurchaseInvoice ADD CreatedBy INT NULL;
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.PurchaseInvoice') AND name = N'CreatedBy' AND is_nullable = 1)
        BEGIN
            UPDATE invoice SET CreatedBy = stockIn.CreatedBy
            FROM dbo.PurchaseInvoice AS invoice
            INNER JOIN dbo.StockIn AS stockIn ON stockIn.Id = invoice.StockInId
            WHERE invoice.CreatedBy IS NULL;
            IF EXISTS (SELECT 1 FROM dbo.PurchaseInvoice WHERE CreatedBy IS NULL)
                THROW 51031, 'Purchase invoice creators cannot be recovered from linked stock-in documents.', 1;
            ALTER TABLE dbo.PurchaseInvoice ALTER COLUMN CreatedBy INT NOT NULL;
        END;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.PurchaseInvoice') AND name = N'FK_PurchaseInvoice_CreatedBy')
            ALTER TABLE dbo.PurchaseInvoice WITH CHECK ADD CONSTRAINT FK_PurchaseInvoice_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id);
        ALTER TABLE dbo.PurchaseInvoice WITH CHECK CHECK CONSTRAINT FK_PurchaseInvoice_CreatedBy;

        IF COL_LENGTH(N'dbo.SalesInvoice', N'CreatedBy') IS NULL
            ALTER TABLE dbo.SalesInvoice ADD CreatedBy INT NULL;
        IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.SalesInvoice') AND name = N'CreatedBy' AND is_nullable = 1)
        BEGIN
            UPDATE invoice SET CreatedBy = stockOut.CreatedBy
            FROM dbo.SalesInvoice AS invoice
            INNER JOIN dbo.StockOut AS stockOut ON stockOut.Id = invoice.StockOutId
            WHERE invoice.CreatedBy IS NULL;
            IF EXISTS (SELECT 1 FROM dbo.SalesInvoice WHERE CreatedBy IS NULL)
                THROW 51032, 'Sales invoice creators cannot be recovered from linked stock-out documents.', 1;
            ALTER TABLE dbo.SalesInvoice ALTER COLUMN CreatedBy INT NOT NULL;
        END;
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.SalesInvoice') AND name = N'FK_SalesInvoice_CreatedBy')
            ALTER TABLE dbo.SalesInvoice WITH CHECK ADD CONSTRAINT FK_SalesInvoice_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id);
        ALTER TABLE dbo.SalesInvoice WITH CHECK CHECK CONSTRAINT FK_SalesInvoice_CreatedBy;

        IF COL_LENGTH(N'dbo.StockAdjustmentLine', N'DraftSerials') IS NULL
            ALTER TABLE dbo.StockAdjustmentLine ADD DraftSerials NVARCHAR(4000) NULL;
        IF COL_LENGTH(N'dbo.StockIn', N'StockCountLineId') IS NULL
            ALTER TABLE dbo.StockIn ADD StockCountLineId INT NULL;
        IF COL_LENGTH(N'dbo.StockIn', N'StockCountSessionId') IS NULL
            ALTER TABLE dbo.StockIn ADD StockCountSessionId INT NULL;
        IF COL_LENGTH(N'dbo.StockOut', N'StockCountLineId') IS NULL
            ALTER TABLE dbo.StockOut ADD StockCountLineId INT NULL;
        IF COL_LENGTH(N'dbo.StockOut', N'StockCountSessionId') IS NULL
            ALTER TABLE dbo.StockOut ADD StockCountSessionId INT NULL;

        -- sản phẩm đã reseed giữ nguyên mã nhưng đổi id; audit nối id cũ với master hiện tại.
        DECLARE @LegacyProductIds TABLE (LegacyProductId INT NOT NULL PRIMARY KEY);
        INSERT INTO @LegacyProductIds (LegacyProductId)
        SELECT legacy.ProductId
        FROM
        (
            SELECT ProductId FROM dbo.StockBalance
            UNION
            SELECT ProductId FROM dbo.StockLedger
            UNION
            SELECT ProductId FROM dbo.StockAdjustmentLine
        ) AS legacy
        LEFT JOIN dbo.Product AS product ON product.Id = legacy.ProductId
        WHERE product.Id IS NULL;

        DECLARE @LegacyProductMap TABLE
        (
            LegacyProductId INT NOT NULL PRIMARY KEY,
            CurrentProductId INT NOT NULL
        );
        ;WITH rankedProductMap AS
        (
            SELECT legacy.LegacyProductId,
                   product.Id AS CurrentProductId,
                   ROW_NUMBER() OVER
                   (
                       PARTITION BY legacy.LegacyProductId
                       ORDER BY audit.PerformedAt DESC, audit.Id DESC
                   ) AS RowNumber
            FROM @LegacyProductIds AS legacy
            INNER JOIN dbo.AuditLog AS audit
                ON audit.EntityName = N'Product'
               AND TRY_CONVERT(INT, audit.EntityId) = legacy.LegacyProductId
            CROSS APPLY
            (
                SELECT COALESCE(NULLIF(audit.BeforeJson, N''), NULLIF(audit.AfterJson, N'')) AS SnapshotJson
            ) AS snapshot
            INNER JOIN dbo.Product AS product
                ON product.ProductCode = JSON_VALUE(
                    CASE WHEN ISJSON(snapshot.SnapshotJson) = 1 THEN snapshot.SnapshotJson END,
                    '$.ProductCode')
        )
        INSERT INTO @LegacyProductMap (LegacyProductId, CurrentProductId)
        SELECT LegacyProductId, CurrentProductId
        FROM rankedProductMap
        WHERE RowNumber = 1;

        IF EXISTS
        (
            SELECT 1
            FROM @LegacyProductIds AS legacy
            LEFT JOIN @LegacyProductMap AS productMap
                ON productMap.LegacyProductId = legacy.LegacyProductId
            WHERE productMap.LegacyProductId IS NULL
        )
            THROW 51029, 'Legacy product references cannot be mapped by audited product code.', 1;

        -- ledger và phiếu điều chỉnh là lịch sử duy nhất nên chỉ đổi khóa về product hiện tại.
        UPDATE ledger SET ProductId = productMap.CurrentProductId
        FROM dbo.StockLedger AS ledger
        INNER JOIN @LegacyProductMap AS productMap
            ON productMap.LegacyProductId = ledger.ProductId;

        UPDATE adjustmentLine SET ProductId = productMap.CurrentProductId
        FROM dbo.StockAdjustmentLine AS adjustmentLine
        INNER JOIN @LegacyProductMap AS productMap
            ON productMap.LegacyProductId = adjustmentLine.ProductId;

        -- balance là projection hiện tại; nếu cặp kho-product mới đã có thì bỏ projection cũ bị orphan.
        DELETE legacyBalance
        FROM dbo.StockBalance AS legacyBalance
        INNER JOIN @LegacyProductMap AS productMap
            ON productMap.LegacyProductId = legacyBalance.ProductId
        WHERE EXISTS
        (
            SELECT 1
            FROM dbo.StockBalance AS currentBalance
            WHERE currentBalance.WarehouseId = legacyBalance.WarehouseId
              AND currentBalance.ProductId = productMap.CurrentProductId
        );

        UPDATE balance SET ProductId = productMap.CurrentProductId
        FROM dbo.StockBalance AS balance
        INNER JOIN @LegacyProductMap AS productMap
            ON productMap.LegacyProductId = balance.ProductId;

        -- đối tác reseed cũng được nối lại bằng mã nghiệp vụ lưu trong audit.
        DECLARE @LegacyPartnerIds TABLE (LegacyPartnerId INT NOT NULL PRIMARY KEY);
        DECLARE @LegacyPartnerMap TABLE
        (
            LegacyPartnerId INT NOT NULL PRIMARY KEY,
            CurrentPartnerId INT NOT NULL
        );

        INSERT INTO @LegacyPartnerIds (LegacyPartnerId)
        SELECT DISTINCT invoice.SupplierId
        FROM dbo.PurchaseInvoice AS invoice
        LEFT JOIN dbo.Supplier AS supplier ON supplier.Id = invoice.SupplierId
        WHERE supplier.Id IS NULL;

        ;WITH rankedPartnerMap AS
        (
            SELECT legacy.LegacyPartnerId,
                   supplier.Id AS CurrentPartnerId,
                   ROW_NUMBER() OVER
                   (
                       PARTITION BY legacy.LegacyPartnerId
                       ORDER BY audit.PerformedAt DESC, audit.Id DESC
                   ) AS RowNumber
            FROM @LegacyPartnerIds AS legacy
            INNER JOIN dbo.AuditLog AS audit
                ON audit.EntityName = N'Supplier'
               AND TRY_CONVERT(INT, audit.EntityId) = legacy.LegacyPartnerId
            CROSS APPLY
            (
                SELECT COALESCE(NULLIF(audit.BeforeJson, N''), NULLIF(audit.AfterJson, N'')) AS SnapshotJson
            ) AS snapshot
            INNER JOIN dbo.Supplier AS supplier
                ON supplier.SupplierCode = JSON_VALUE(
                    CASE WHEN ISJSON(snapshot.SnapshotJson) = 1 THEN snapshot.SnapshotJson END,
                    '$.SupplierCode')
        )
        INSERT INTO @LegacyPartnerMap (LegacyPartnerId, CurrentPartnerId)
        SELECT LegacyPartnerId, CurrentPartnerId
        FROM rankedPartnerMap
        WHERE RowNumber = 1;

        IF EXISTS
        (
            SELECT 1
            FROM @LegacyPartnerIds AS legacy
            LEFT JOIN @LegacyPartnerMap AS partnerMap
                ON partnerMap.LegacyPartnerId = legacy.LegacyPartnerId
            WHERE partnerMap.LegacyPartnerId IS NULL
        )
            THROW 51030, 'Legacy partner references cannot be mapped by audited business code.', 1;

        UPDATE invoice SET SupplierId = partnerMap.CurrentPartnerId
        FROM dbo.PurchaseInvoice AS invoice
        INNER JOIN @LegacyPartnerMap AS partnerMap
            ON partnerMap.LegacyPartnerId = invoice.SupplierId;

        DELETE FROM @LegacyPartnerIds;
        DELETE FROM @LegacyPartnerMap;

        INSERT INTO @LegacyPartnerIds (LegacyPartnerId)
        SELECT DISTINCT invoice.CustomerId
        FROM dbo.SalesInvoice AS invoice
        LEFT JOIN dbo.Customer AS customer ON customer.Id = invoice.CustomerId
        WHERE customer.Id IS NULL;

        ;WITH rankedPartnerMap AS
        (
            SELECT legacy.LegacyPartnerId,
                   customer.Id AS CurrentPartnerId,
                   ROW_NUMBER() OVER
                   (
                       PARTITION BY legacy.LegacyPartnerId
                       ORDER BY audit.PerformedAt DESC, audit.Id DESC
                   ) AS RowNumber
            FROM @LegacyPartnerIds AS legacy
            INNER JOIN dbo.AuditLog AS audit
                ON audit.EntityName = N'Customer'
               AND TRY_CONVERT(INT, audit.EntityId) = legacy.LegacyPartnerId
            CROSS APPLY
            (
                SELECT COALESCE(NULLIF(audit.BeforeJson, N''), NULLIF(audit.AfterJson, N'')) AS SnapshotJson
            ) AS snapshot
            INNER JOIN dbo.Customer AS customer
                ON customer.CustomerCode = JSON_VALUE(
                    CASE WHEN ISJSON(snapshot.SnapshotJson) = 1 THEN snapshot.SnapshotJson END,
                    '$.CustomerCode')
        )
        INSERT INTO @LegacyPartnerMap (LegacyPartnerId, CurrentPartnerId)
        SELECT LegacyPartnerId, CurrentPartnerId
        FROM rankedPartnerMap
        WHERE RowNumber = 1;

        IF EXISTS
        (
            SELECT 1
            FROM @LegacyPartnerIds AS legacy
            LEFT JOIN @LegacyPartnerMap AS partnerMap
                ON partnerMap.LegacyPartnerId = legacy.LegacyPartnerId
            WHERE partnerMap.LegacyPartnerId IS NULL
        )
            THROW 51030, 'Legacy partner references cannot be mapped by audited business code.', 1;

        UPDATE invoice SET CustomerId = partnerMap.CurrentPartnerId
        FROM dbo.SalesInvoice AS invoice
        INNER JOIN @LegacyPartnerMap AS partnerMap
            ON partnerMap.LegacyPartnerId = invoice.CustomerId;

        -- model C# dùng giá trị bắt buộc; dữ liệu null cũ được chuẩn hóa trước khi siết column.
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SalesInvoice') AND name = N'IX_SalesInvoice_PaymentStatus_InvoiceDate')
            DROP INDEX IX_SalesInvoice_PaymentStatus_InvoiceDate ON dbo.SalesInvoice;
        IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PurchaseInvoice') AND name = N'IX_PurchaseInvoice_PaymentStatus_InvoiceDate')
            DROP INDEX IX_PurchaseInvoice_PaymentStatus_InvoiceDate ON dbo.PurchaseInvoice;
        UPDATE dbo.SalesInvoice SET PaidAmount = 0 WHERE PaidAmount IS NULL;
        UPDATE dbo.SalesInvoice
        SET PaymentStatus = CASE
            WHEN PaidAmount >= GrandTotal AND GrandTotal > 0 THEN N'Paid'
            WHEN PaidAmount > 0 THEN N'PartiallyPaid'
            ELSE N'Unpaid'
        END
        WHERE PaymentStatus IS NULL OR PaymentStatus NOT IN (N'Unpaid', N'PartiallyPaid', N'Paid', N'Overdue');
        ALTER TABLE dbo.SalesInvoice ALTER COLUMN PaidAmount DECIMAL(18,2) NOT NULL;
        ALTER TABLE dbo.SalesInvoice ALTER COLUMN PaymentStatus NVARCHAR(50) NOT NULL;

        UPDATE dbo.PurchaseInvoice SET PaidAmount = 0 WHERE PaidAmount IS NULL;
        UPDATE dbo.PurchaseInvoice
        SET PaymentStatus = CASE
            WHEN PaidAmount >= GrandTotal AND GrandTotal > 0 THEN N'Paid'
            WHEN PaidAmount > 0 THEN N'PartiallyPaid'
            ELSE N'Unpaid'
        END
        WHERE PaymentStatus IS NULL OR PaymentStatus NOT IN (N'Unpaid', N'PartiallyPaid', N'Paid', N'Overdue');
        ALTER TABLE dbo.PurchaseInvoice ALTER COLUMN PaidAmount DECIMAL(18,2) NOT NULL;
        ALTER TABLE dbo.PurchaseInvoice ALTER COLUMN PaymentStatus NVARCHAR(50) NOT NULL;

        -- WITH CHECK CHECK vừa kiểm dữ liệu hiện tại vừa bật kiểm tra cho các lần ghi sau.
        ALTER TABLE dbo.Product WITH CHECK CHECK CONSTRAINT FK_Product_Category;
        ALTER TABLE dbo.Product WITH CHECK CHECK CONSTRAINT FK_Product_Brand;
        ALTER TABLE dbo.Product WITH CHECK CHECK CONSTRAINT FK_Product_DefaultUnit;
        ALTER TABLE dbo.ProductUnit WITH CHECK CHECK CONSTRAINT FK_ProductUnit_Product;
        ALTER TABLE dbo.ProductUnit WITH CHECK CHECK CONSTRAINT FK_ProductUnit_Unit;
        ALTER TABLE dbo.StockBalance WITH CHECK CHECK CONSTRAINT FK_StockBalance_Warehouse;
        ALTER TABLE dbo.StockBalance WITH CHECK CHECK CONSTRAINT FK_StockBalance_Product;
        ALTER TABLE dbo.StockInLine WITH CHECK CHECK CONSTRAINT FK_StockInLine_StockIn;
        ALTER TABLE dbo.StockOutLine WITH CHECK CHECK CONSTRAINT FK_StockOutLine_StockOut;
        ALTER TABLE dbo.PurchaseInvoice WITH CHECK CHECK CONSTRAINT FK_PurchaseInvoice_Supplier;
        ALTER TABLE dbo.SalesInvoice WITH CHECK CHECK CONSTRAINT FK_SalesInvoice_Customer;
        ALTER TABLE dbo.Product WITH CHECK CHECK CONSTRAINT CK_Product_DefaultPrice_NonNegative;
        ALTER TABLE dbo.ProductUnit WITH CHECK CHECK CONSTRAINT CK_ProductUnit_ConversionFactor_Positive;
        ALTER TABLE dbo.StockBalance WITH CHECK CHECK CONSTRAINT CK_StockBalance_OnHand_NonNegative;


        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.AuditArchiveManifest') AND name = N'FK_AuditArchiveManifest_Actor')
            ALTER TABLE dbo.AuditArchiveManifest WITH CHECK ADD CONSTRAINT FK_AuditArchiveManifest_Actor FOREIGN KEY (ActorId) REFERENCES dbo.AppUser(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'FK_StockTransfer_FromWarehouse')
            ALTER TABLE dbo.StockTransfer WITH CHECK ADD CONSTRAINT FK_StockTransfer_FromWarehouse FOREIGN KEY (FromWarehouseId) REFERENCES dbo.Warehouse(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'FK_StockTransfer_ToWarehouse')
            ALTER TABLE dbo.StockTransfer WITH CHECK ADD CONSTRAINT FK_StockTransfer_ToWarehouse FOREIGN KEY (ToWarehouseId) REFERENCES dbo.Warehouse(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'FK_StockTransfer_CreatedBy')
            ALTER TABLE dbo.StockTransfer WITH CHECK ADD CONSTRAINT FK_StockTransfer_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'FK_StockTransfer_ApprovedBy')
            ALTER TABLE dbo.StockTransfer WITH CHECK ADD CONSTRAINT FK_StockTransfer_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'FK_StockTransfer_PostedBy')
            ALTER TABLE dbo.StockTransfer WITH CHECK ADD CONSTRAINT FK_StockTransfer_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransferLine') AND name = N'FK_StockTransferLine_StockTransfer')
            ALTER TABLE dbo.StockTransferLine WITH CHECK ADD CONSTRAINT FK_StockTransferLine_StockTransfer FOREIGN KEY (StockTransferId) REFERENCES dbo.StockTransfer(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransferLine') AND name = N'FK_StockTransferLine_Product')
            ALTER TABLE dbo.StockTransferLine WITH CHECK ADD CONSTRAINT FK_StockTransferLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockTransferLine') AND name = N'FK_StockTransferLine_Unit')
            ALTER TABLE dbo.StockTransferLine WITH CHECK ADD CONSTRAINT FK_StockTransferLine_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id);
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockIn') AND name = N'FK_StockIn_StockCountLine')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.StockIn WITH CHECK ADD CONSTRAINT FK_StockIn_StockCountLine FOREIGN KEY (StockCountLineId) REFERENCES dbo.StockCountLine(Id);';
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockIn') AND name = N'FK_StockIn_StockCountSession')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.StockIn WITH CHECK ADD CONSTRAINT FK_StockIn_StockCountSession FOREIGN KEY (StockCountSessionId) REFERENCES dbo.StockCountSession(Id);';
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockOut') AND name = N'FK_StockOut_StockCountLine')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.StockOut WITH CHECK ADD CONSTRAINT FK_StockOut_StockCountLine FOREIGN KEY (StockCountLineId) REFERENCES dbo.StockCountLine(Id);';
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.StockOut') AND name = N'FK_StockOut_StockCountSession')
            EXEC sys.sp_executesql N'ALTER TABLE dbo.StockOut WITH CHECK ADD CONSTRAINT FK_StockOut_StockCountSession FOREIGN KEY (StockCountSessionId) REFERENCES dbo.StockCountSession(Id);';

        ALTER TABLE dbo.AuditArchiveManifest WITH CHECK CHECK CONSTRAINT FK_AuditArchiveManifest_Actor;
        ALTER TABLE dbo.StockTransfer WITH CHECK CHECK CONSTRAINT FK_StockTransfer_FromWarehouse;
        ALTER TABLE dbo.StockTransfer WITH CHECK CHECK CONSTRAINT FK_StockTransfer_ToWarehouse;
        ALTER TABLE dbo.StockTransfer WITH CHECK CHECK CONSTRAINT FK_StockTransfer_CreatedBy;
        ALTER TABLE dbo.StockTransfer WITH CHECK CHECK CONSTRAINT FK_StockTransfer_ApprovedBy;
        ALTER TABLE dbo.StockTransfer WITH CHECK CHECK CONSTRAINT FK_StockTransfer_PostedBy;
        ALTER TABLE dbo.StockTransferLine WITH CHECK CHECK CONSTRAINT FK_StockTransferLine_StockTransfer;
        ALTER TABLE dbo.StockTransferLine WITH CHECK CHECK CONSTRAINT FK_StockTransferLine_Product;
        ALTER TABLE dbo.StockTransferLine WITH CHECK CHECK CONSTRAINT FK_StockTransferLine_Unit;
        ALTER TABLE dbo.StockIn WITH CHECK CHECK CONSTRAINT FK_StockIn_StockCountLine;
        ALTER TABLE dbo.StockIn WITH CHECK CHECK CONSTRAINT FK_StockIn_StockCountSession;
        ALTER TABLE dbo.StockOut WITH CHECK CHECK CONSTRAINT FK_StockOut_StockCountLine;
        ALTER TABLE dbo.StockOut WITH CHECK CHECK CONSTRAINT FK_StockOut_StockCountSession;

        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditArchiveManifest') AND name = N'IX_AuditArchiveManifest_ActorId')
            CREATE INDEX IX_AuditArchiveManifest_ActorId ON dbo.AuditArchiveManifest(ActorId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditArchiveManifest') AND name = N'IX_AuditArchiveManifest_CreatedAtUtc')
            CREATE INDEX IX_AuditArchiveManifest_CreatedAtUtc ON dbo.AuditArchiveManifest(CreatedAtUtc);
        IF COL_LENGTH(N'dbo.AuditArchiveManifest', N'OperationId') IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AuditArchiveManifest') AND name = N'UX_AuditArchiveManifest_OperationId')
            CREATE UNIQUE INDEX UX_AuditArchiveManifest_OperationId ON dbo.AuditArchiveManifest(OperationId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockIn') AND name = N'IX_StockIn_StockCountSessionId')
            EXEC sys.sp_executesql N'CREATE INDEX IX_StockIn_StockCountSessionId ON dbo.StockIn(StockCountSessionId);';
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockIn') AND name = N'UX_StockIn_StockCountLineId')
            EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_StockIn_StockCountLineId ON dbo.StockIn(StockCountLineId) WHERE StockCountLineId IS NOT NULL;';
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockOut') AND name = N'IX_StockOut_StockCountSessionId')
            EXEC sys.sp_executesql N'CREATE INDEX IX_StockOut_StockCountSessionId ON dbo.StockOut(StockCountSessionId);';
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockOut') AND name = N'UX_StockOut_StockCountLineId')
            EXEC sys.sp_executesql N'CREATE UNIQUE INDEX UX_StockOut_StockCountLineId ON dbo.StockOut(StockCountLineId) WHERE StockCountLineId IS NOT NULL;';
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockTransfer') AND name = N'UX_StockTransfer_DocumentCode')
            CREATE UNIQUE INDEX UX_StockTransfer_DocumentCode ON dbo.StockTransfer(DocumentCode);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockTransferLine') AND name = N'IX_StockTransferLine_StockTransferId')
            CREATE INDEX IX_StockTransferLine_StockTransferId ON dbo.StockTransferLine(StockTransferId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockTransferLine') AND name = N'IX_StockTransferLine_ProductId')
            CREATE INDEX IX_StockTransferLine_ProductId ON dbo.StockTransferLine(ProductId);
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.StockTransferLine') AND name = N'IX_StockTransferLine_UnitId')
            CREATE INDEX IX_StockTransferLine_UnitId ON dbo.StockTransferLine(UnitId);
        """;

    public static string SchemaMetadata => SingleBatch("SchemaMetadataSql");
    public static string SchemaVersion1 => SingleBatch("SchemaVersion1Sql");
    public static string SchemaVersion2 => SingleBatch("SchemaVersion2Sql");
    public static string SchemaVersion3 => SingleBatch("SchemaVersion3Sql");
    public static string SchemaVersion4 => SingleBatch("SchemaVersion4Sql");
    public static string SchemaVersion5 => SingleBatch("SchemaVersion5Sql");
    public static string SchemaVersion6 => ReadEmbeddedText("WarePro.Core.Resources.v6-common-write-safety.sql");
    public static string SchemaVersion7 => ReadEmbeddedText("WarePro.Core.Resources.v7-invoice-void-open-claim.sql");
    public static string SchemaVersion8 => ReadEmbeddedText("WarePro.Core.Resources.v8-unique-invoice-stock-links.sql");
    public static string SchemaVersion9 => ReadEmbeddedText("WarePro.Core.Resources.v9-nullable-login-audit-performer.sql");
    public static string SchemaVersion10 => ReadEmbeddedText("WarePro.Core.Resources.v10-nullable-serial-last-stock-in.sql");
    public static string SchemaVersion11 => ReadEmbeddedText("WarePro.Core.Resources.v11-document-number-counter.sql");
    public static string SchemaArchiveReplay => SingleBatch("SchemaArchiveReplaySql");

    public static string ShapeValidationPredicate => """
        NOT EXISTS
        (
            SELECT expected.TableName, expected.ColumnName, expected.TypeName,
                   expected.MaxLength, expected.Precision, expected.Scale, expected.IsNullable
            FROM (VALUES
                (N'AuditLog', N'PerformedBy', N'int', 4, 10, 0, 1),
                (N'AuditArchiveManifest', N'Id', N'int', 4, 10, 0, 0),
                (N'AuditArchiveManifest', N'OperationId', N'uniqueidentifier', 16, 0, 0, 0),
                (N'AuditArchiveManifest', N'ActorId', N'int', 4, 10, 0, 0),
                (N'AuditArchiveManifest', N'RangeStartUtc', N'datetime2', 6, 19, 0, 0),
                (N'AuditArchiveManifest', N'RangeEndUtc', N'datetime2', 6, 19, 0, 0),
                (N'AuditArchiveManifest', N'RowCount', N'int', 4, 10, 0, 0),
                (N'AuditArchiveManifest', N'FileName', N'nvarchar', 520, 0, 0, 0),
                (N'AuditArchiveManifest', N'Sha256Hash', N'nchar', 128, 0, 0, 0),
                (N'AuditArchiveManifest', N'CreatedAtUtc', N'datetime2', 6, 19, 0, 0),
                (N'AuditArchiveManifest', N'RowVersion', N'timestamp', 8, 0, 0, 0),
                (N'StockTransfer', N'Id', N'int', 4, 10, 0, 0),
                (N'StockTransfer', N'DocumentCode', N'nvarchar', 100, 0, 0, 0),
                (N'StockTransfer', N'FromWarehouseId', N'int', 4, 10, 0, 0),
                (N'StockTransfer', N'ToWarehouseId', N'int', 4, 10, 0, 0),
                (N'StockTransfer', N'Status', N'nvarchar', 100, 0, 0, 0),
                (N'StockTransfer', N'TransferDate', N'datetime2', 6, 19, 0, 0),
                (N'StockTransfer', N'Notes', N'nvarchar', 1000, 0, 0, 1),
                (N'StockTransfer', N'CreatedBy', N'int', 4, 10, 0, 0),
                (N'StockTransfer', N'ApprovedBy', N'int', 4, 10, 0, 1),
                (N'StockTransfer', N'PostedBy', N'int', 4, 10, 0, 1),
                (N'StockTransfer', N'CreatedAt', N'datetime2', 6, 19, 0, 0),
                (N'StockTransfer', N'ApprovedAt', N'datetime2', 6, 19, 0, 1),
                (N'StockTransfer', N'PostedAt', N'datetime2', 6, 19, 0, 1),
                (N'StockTransfer', N'UpdatedAt', N'datetime2', 6, 19, 0, 1),
                (N'StockTransfer', N'UpdatedBy', N'int', 4, 10, 0, 1),
                (N'StockTransfer', N'RowVersion', N'timestamp', 8, 0, 0, 0),
                (N'StockTransferLine', N'Id', N'int', 4, 10, 0, 0),
                (N'StockTransferLine', N'StockTransferId', N'int', 4, 10, 0, 0),
                (N'StockTransferLine', N'ProductId', N'int', 4, 10, 0, 0),
                (N'StockTransferLine', N'UnitId', N'int', 4, 10, 0, 0),
                (N'StockTransferLine', N'Quantity', N'decimal', 9, 18, 2, 0),
                (N'StockTransferLine', N'BaseQuantity', N'decimal', 9, 18, 2, 0),
                (N'StockTransferLine', N'RowVersion', N'timestamp', 8, 0, 0, 0),
                (N'ProductSerial', N'LastStockInLineId', N'int', 4, 10, 0, 1),
                (N'DocumentNumberCounter', N'DocumentType', N'nvarchar', 64, 0, 0, 0),
                (N'DocumentNumberCounter', N'BusinessDate', N'date', 3, 10, 0, 0),
                (N'DocumentNumberCounter', N'LastValue', N'bigint', 8, 19, 0, 0),
                (N'DocumentNumberCounter', N'RowVersion', N'timestamp', 8, 0, 0, 0)
            ) AS expected(TableName, ColumnName, TypeName, MaxLength, Precision, Scale, IsNullable)
            EXCEPT
            SELECT OBJECT_NAME(columns.object_id), columns.name, TYPE_NAME(columns.system_type_id),
                   columns.max_length, columns.precision, columns.scale, columns.is_nullable
            FROM sys.columns AS columns
            WHERE columns.object_id IN
            (
                OBJECT_ID(N'dbo.AuditLog'),
                OBJECT_ID(N'dbo.AuditArchiveManifest'),
                OBJECT_ID(N'dbo.StockTransfer'),
                OBJECT_ID(N'dbo.StockTransferLine'),
                OBJECT_ID(N'dbo.ProductSerial'),
                OBJECT_ID(N'dbo.DocumentNumberCounter')
            )
        )
        AND NOT EXISTS
        (
            SELECT expected.TableName, expected.IndexName, expected.IsUnique, expected.ColumnName
            FROM (VALUES
                (N'AuditArchiveManifest', N'UX_AuditArchiveManifest_OperationId', 1, N'OperationId'),
                (N'AuditArchiveManifest', N'IX_AuditArchiveManifest_ActorId', 0, N'ActorId'),
                (N'AuditArchiveManifest', N'IX_AuditArchiveManifest_CreatedAtUtc', 0, N'CreatedAtUtc'),
                (N'StockTransfer', N'UX_StockTransfer_DocumentCode', 1, N'DocumentCode'),
                (N'StockTransferLine', N'IX_StockTransferLine_StockTransferId', 0, N'StockTransferId'),
                (N'StockTransferLine', N'IX_StockTransferLine_ProductId', 0, N'ProductId'),
                (N'StockTransferLine', N'IX_StockTransferLine_UnitId', 0, N'UnitId')
            ) AS expected(TableName, IndexName, IsUnique, ColumnName)
            EXCEPT
            SELECT OBJECT_NAME(indexes.object_id), indexes.name, indexes.is_unique, columns.name
            FROM sys.indexes AS indexes
            INNER JOIN sys.index_columns AS index_columns
                ON index_columns.object_id = indexes.object_id
               AND index_columns.index_id = indexes.index_id
               AND index_columns.key_ordinal = 1
            INNER JOIN sys.columns AS columns
                ON columns.object_id = index_columns.object_id
               AND columns.column_id = index_columns.column_id
            WHERE indexes.object_id IN
            (
                OBJECT_ID(N'dbo.AuditArchiveManifest'),
                OBJECT_ID(N'dbo.StockTransfer'),
                OBJECT_ID(N'dbo.StockTransferLine')
            )
              AND indexes.is_disabled = 0
              AND (SELECT COUNT(*) FROM sys.index_columns AS keys
                   WHERE keys.object_id = indexes.object_id
                     AND keys.index_id = indexes.index_id
                     AND keys.key_ordinal > 0) = 1
        )
        AND NOT EXISTS
        (
            SELECT expected.ParentTable, expected.ParentColumn, expected.ReferencedTable, expected.ReferencedColumn
            FROM (VALUES
                (N'AuditArchiveManifest', N'ActorId', N'AppUser', N'Id'),
                (N'StockTransfer', N'FromWarehouseId', N'Warehouse', N'Id'),
                (N'StockTransfer', N'ToWarehouseId', N'Warehouse', N'Id'),
                (N'StockTransfer', N'CreatedBy', N'AppUser', N'Id'),
                (N'StockTransfer', N'ApprovedBy', N'AppUser', N'Id'),
                (N'StockTransfer', N'PostedBy', N'AppUser', N'Id'),
                (N'StockTransferLine', N'StockTransferId', N'StockTransfer', N'Id'),
                (N'StockTransferLine', N'ProductId', N'Product', N'Id'),
                (N'StockTransferLine', N'UnitId', N'Unit', N'Id')
            ) AS expected(ParentTable, ParentColumn, ReferencedTable, ReferencedColumn)
            EXCEPT
            SELECT OBJECT_NAME(foreign_columns.parent_object_id), parent_columns.name,
                   OBJECT_NAME(foreign_columns.referenced_object_id), referenced_columns.name
            FROM sys.foreign_key_columns AS foreign_columns
            INNER JOIN sys.foreign_keys AS foreign_keys ON foreign_keys.object_id = foreign_columns.constraint_object_id
            INNER JOIN sys.columns AS parent_columns ON parent_columns.object_id = foreign_columns.parent_object_id AND parent_columns.column_id = foreign_columns.parent_column_id
            INNER JOIN sys.columns AS referenced_columns ON referenced_columns.object_id = foreign_columns.referenced_object_id AND referenced_columns.column_id = foreign_columns.referenced_column_id
            WHERE foreign_keys.is_disabled = 0 AND foreign_keys.is_not_trusted = 0
        )
        AND NOT EXISTS
        (
            SELECT expected.TableName, expected.ColumnName, expected.TypeName,
                   expected.MaxLength, expected.Precision, expected.Scale, expected.IsNullable
            FROM (VALUES
                (N'__WareProDatabaseIdentity', N'Id', N'int', 4, 10, 0, 0),
                (N'__WareProDatabaseIdentity', N'ProductId', N'uniqueidentifier', 16, 0, 0, 0),
                (N'__WareProDatabaseIdentity', N'ProductName', N'nvarchar', 64, 0, 0, 0),
                (N'__WareProSchemaVersion', N'Id', N'int', 4, 10, 0, 0),
                (N'__WareProSchemaVersion', N'Version', N'int', 4, 10, 0, 0),
                (N'__WareProSchemaVersion', N'MinimumClientVersion', N'nvarchar', 64, 0, 0, 0),
                (N'__WareProSchemaVersion', N'AppliedByAppVersion', N'nvarchar', 128, 0, 0, 0),
                (N'__WareProSchemaVersion', N'UpdatedAt', N'datetime2', 8, 27, 7, 0),
                (N'__WareProClientSession', N'SessionId', N'uniqueidentifier', 16, 0, 0, 0),
                (N'__WareProClientSession', N'MachineName', N'nvarchar', 510, 0, 0, 0),
                (N'__WareProClientSession', N'ProcessId', N'int', 4, 10, 0, 0),
                (N'__WareProClientSession', N'AppVersion', N'nvarchar', 64, 0, 0, 0),
                (N'__WareProClientSession', N'StartedAtUtc', N'datetime2', 6, 19, 0, 0),
                (N'__WareProClientSession', N'LastSeenUtc', N'datetime2', 6, 19, 0, 0),
                (N'__WareProClientSession', N'RowVersion', N'timestamp', 8, 0, 0, 0),
                (N'Product', N'ProductCode', N'nvarchar', 100, 0, 0, 0),
                (N'Product', N'DisplayName', N'nvarchar', 400, 0, 0, 0),
                (N'Product', N'CategoryId', N'int', 4, 10, 0, 0),
                (N'Product', N'BrandId', N'int', 4, 10, 0, 0),
                (N'Product', N'DefaultUnitId', N'int', 4, 10, 0, 0),
                (N'Product', N'DefaultPrice', N'decimal', 9, 18, 2, 0),
                (N'Product', N'WarrantyPeriodMonths', N'int', 4, 10, 0, 0),
                (N'ProductUnit', N'ProductId', N'int', 4, 10, 0, 0),
                (N'ProductUnit', N'UnitId', N'int', 4, 10, 0, 0),
                (N'ProductUnit', N'ConversionFactor', N'decimal', 9, 18, 6, 0),
                (N'ProductUnit', N'IsBaseUnit', N'bit', 1, 1, 0, 0),
                (N'StockBalance', N'WarehouseId', N'int', 4, 10, 0, 0),
                (N'StockBalance', N'ProductId', N'int', 4, 10, 0, 0),
                (N'StockBalance', N'OnHandQuantity', N'decimal', 9, 18, 2, 0),
                (N'StockBalance', N'AvailableQuantity', N'decimal', 9, 18, 2, 0),
                (N'StockBalance', N'ReservedQuantity', N'decimal', 9, 18, 2, 0),
                (N'StockInLine', N'StockInId', N'int', 4, 10, 0, 0),
                (N'StockInLine', N'ProductId', N'int', 4, 10, 0, 0),
                (N'StockInLine', N'BaseQuantity', N'decimal', 9, 18, 2, 0),
                (N'StockOutLine', N'StockOutId', N'int', 4, 10, 0, 0),
                (N'StockOutLine', N'ProductId', N'int', 4, 10, 0, 0),
                (N'StockOutLine', N'BaseQuantity', N'decimal', 9, 18, 2, 0),
                (N'StockLedger', N'WarehouseId', N'int', 4, 10, 0, 0),
                (N'StockLedger', N'ProductId', N'int', 4, 10, 0, 0),
                (N'StockLedger', N'SourceDocumentType', N'nvarchar', 100, 0, 0, 0),
                (N'StockLedger', N'SourceDocumentId', N'int', 4, 10, 0, 0),
                (N'StockLedger', N'Quantity', N'decimal', 9, 18, 2, 0),
                (N'StockAdjustmentLine', N'DraftSerials', N'nvarchar', 8000, 0, 0, 1),
                (N'StockIn', N'StockCountLineId', N'int', 4, 10, 0, 1),
                (N'StockIn', N'StockCountSessionId', N'int', 4, 10, 0, 1),
                (N'StockOut', N'StockCountLineId', N'int', 4, 10, 0, 1),
                (N'StockOut', N'StockCountSessionId', N'int', 4, 10, 0, 1),
                (N'PurchaseInvoice', N'InvoiceCode', N'nvarchar', 100, 0, 0, 0),
                (N'PurchaseInvoice', N'SupplierId', N'int', 4, 10, 0, 0),
                (N'PurchaseInvoice', N'GrandTotal', N'decimal', 9, 18, 2, 0),
                (N'PurchaseInvoice', N'PaidAmount', N'decimal', 9, 18, 2, 0),
                (N'PurchaseInvoice', N'PaymentStatus', N'nvarchar', 100, 0, 0, 0),
                (N'PurchaseInvoice', N'Status', N'nvarchar', 40, 0, 0, 0),
                (N'PurchaseInvoice', N'CreatedBy', N'int', 4, 10, 0, 0),
                (N'SalesInvoice', N'InvoiceCode', N'nvarchar', 100, 0, 0, 0),
                (N'SalesInvoice', N'CustomerId', N'int', 4, 10, 0, 0),
                (N'SalesInvoice', N'GrandTotal', N'decimal', 9, 18, 2, 0),
                (N'SalesInvoice', N'PaidAmount', N'decimal', 9, 18, 2, 0),
                (N'SalesInvoice', N'PaymentStatus', N'nvarchar', 100, 0, 0, 0),
                (N'SalesInvoice', N'Status', N'nvarchar', 40, 0, 0, 0),
                (N'SalesInvoice', N'CreatedBy', N'int', 4, 10, 0, 0),
                (N'WarrantyClaim', N'OpenProductSerialId', N'int', 4, 10, 0, 1)
            ) AS expected(TableName, ColumnName, TypeName, MaxLength, Precision, Scale, IsNullable)
            EXCEPT
            SELECT OBJECT_NAME(columns.object_id), columns.name, TYPE_NAME(columns.system_type_id),
                   columns.max_length, columns.precision, columns.scale, columns.is_nullable
            FROM sys.columns AS columns
        )
        AND NOT EXISTS
        (
            SELECT expected.TableName
            FROM (VALUES
                (N'AppUser'), (N'AuditArchiveManifest'), (N'Brand'), (N'Category'),
                (N'Customer'), (N'Product'), (N'ProductSerial'), (N'ProductUnit'),
                (N'PurchaseInvoice'), (N'PurchaseInvoiceLine'), (N'SalesInvoice'), (N'SalesInvoiceLine'),
                (N'StockAdjustment'), (N'StockAdjustmentLine'), (N'StockBalance'),
                (N'StockCountSession'), (N'StockCountLine'), (N'StockIn'), (N'StockInLine'),
                (N'StockOut'), (N'StockOutLine'), (N'StockTransfer'),
                (N'StockTransferLine'), (N'Supplier'), (N'Unit'), (N'Warehouse'),
                (N'WarrantyClaim'), (N'WarrantyCoverage')
            ) AS expected(TableName)
            EXCEPT
            SELECT OBJECT_NAME(columns.object_id)
            FROM sys.columns AS columns
            WHERE columns.name = N'RowVersion'
              AND TYPE_NAME(columns.system_type_id) = N'timestamp'
              AND columns.is_nullable = 0
        )
        AND NOT EXISTS
        (
            SELECT expected.ParentTable, expected.ParentColumn, expected.ReferencedTable, expected.ReferencedColumn
            FROM (VALUES
                (N'Product', N'CategoryId', N'Category', N'Id'),
                (N'Product', N'BrandId', N'Brand', N'Id'),
                (N'Product', N'DefaultUnitId', N'Unit', N'Id'),
                (N'ProductUnit', N'ProductId', N'Product', N'Id'),
                (N'ProductUnit', N'UnitId', N'Unit', N'Id'),
                (N'StockBalance', N'WarehouseId', N'Warehouse', N'Id'),
                (N'StockBalance', N'ProductId', N'Product', N'Id'),
                (N'StockInLine', N'StockInId', N'StockIn', N'Id'),
                (N'StockOutLine', N'StockOutId', N'StockOut', N'Id'),
                (N'PurchaseInvoice', N'SupplierId', N'Supplier', N'Id'),
                (N'PurchaseInvoice', N'CreatedBy', N'AppUser', N'Id'),
                (N'SalesInvoice', N'CustomerId', N'Customer', N'Id'),
                (N'SalesInvoice', N'CreatedBy', N'AppUser', N'Id'),
                (N'WarrantyClaim', N'WarrantyCoverageId', N'WarrantyCoverage', N'Id'),
                (N'WarrantyClaim', N'ProductSerialId', N'WarrantyCoverage', N'ProductSerialId'),
                (N'StockIn', N'StockCountLineId', N'StockCountLine', N'Id'),
                (N'StockIn', N'StockCountSessionId', N'StockCountSession', N'Id'),
                (N'StockOut', N'StockCountLineId', N'StockCountLine', N'Id'),
                (N'StockOut', N'StockCountSessionId', N'StockCountSession', N'Id')
            ) AS expected(ParentTable, ParentColumn, ReferencedTable, ReferencedColumn)
            EXCEPT
            SELECT OBJECT_NAME(fkc.parent_object_id), pc.name,
                   OBJECT_NAME(fkc.referenced_object_id), rc.name
            FROM sys.foreign_key_columns AS fkc
            INNER JOIN sys.foreign_keys AS fk ON fk.object_id = fkc.constraint_object_id
            INNER JOIN sys.columns AS pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
            INNER JOIN sys.columns AS rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
            WHERE fk.is_disabled = 0 AND fk.is_not_trusted = 0
        )
        AND NOT EXISTS
        (
            SELECT expected.ConstraintName
            FROM (VALUES
                (N'FK_Product_Category'), (N'FK_Product_Brand'), (N'FK_Product_DefaultUnit'),
                (N'FK_StockInLine_StockIn'), (N'FK_StockOutLine_StockOut'),
                (N'FK_StockIn_StockCountLine'), (N'FK_StockIn_StockCountSession'),
                (N'FK_StockOut_StockCountLine'), (N'FK_StockOut_StockCountSession'),
                (N'FK_WarrantyClaim_Coverage')
            ) AS expected(ConstraintName)
            EXCEPT
            SELECT name FROM sys.foreign_keys WHERE is_disabled = 0 AND is_not_trusted = 0
        )
        AND NOT EXISTS
        (
            SELECT expected.ConstraintName
            FROM (VALUES
                (N'CK_Product_DefaultPrice_NonNegative'),
                (N'CK_ProductUnit_ConversionFactor_Positive'),
                (N'CK_StockBalance_OnHand_NonNegative'),
                (N'CK_PurchaseInvoice_PaymentStatus'),
                (N'CK_SalesInvoice_PaymentStatus'),
                (N'CK_PurchaseInvoice_Status'),
                (N'CK_SalesInvoice_Status')
            ) AS expected(ConstraintName)
            EXCEPT
            SELECT name FROM sys.check_constraints WHERE is_disabled = 0 AND is_not_trusted = 0
        )
        AND NOT EXISTS
        (
            SELECT expected.TableName, expected.IndexName, expected.IsUnique,
                   expected.KeyOrdinal, expected.ColumnName, expected.KeyCount
            FROM (VALUES
                (N'Product', N'UX_Product_ProductCode', 1, 1, N'ProductCode', 1),
                (N'StockBalance', N'UX_StockBalance_Warehouse_Product', 1, 1, N'WarehouseId', 2),
                (N'StockBalance', N'UX_StockBalance_Warehouse_Product', 1, 2, N'ProductId', 2),
                (N'PurchaseInvoice', N'IX_PurchaseInvoice_PaymentStatus_InvoiceDate', 0, 1, N'PaymentStatus', 2),
                (N'PurchaseInvoice', N'IX_PurchaseInvoice_PaymentStatus_InvoiceDate', 0, 2, N'InvoiceDate', 2),
                (N'PurchaseInvoice', N'IX_PurchaseInvoice_Status_InvoiceDate', 0, 1, N'Status', 2),
                (N'PurchaseInvoice', N'IX_PurchaseInvoice_Status_InvoiceDate', 0, 2, N'InvoiceDate', 2),
                (N'SalesInvoice', N'IX_SalesInvoice_PaymentStatus_InvoiceDate', 0, 1, N'PaymentStatus', 2),
                (N'SalesInvoice', N'IX_SalesInvoice_PaymentStatus_InvoiceDate', 0, 2, N'InvoiceDate', 2),
                (N'SalesInvoice', N'IX_SalesInvoice_Status_InvoiceDate', 0, 1, N'Status', 2),
                (N'PurchaseInvoice', N'UX_PurchaseInvoice_StockInId', 1, 1, N'StockInId', 1),
                (N'SalesInvoice', N'UX_SalesInvoice_StockOutId', 1, 1, N'StockOutId', 1),
                (N'SalesInvoice', N'IX_SalesInvoice_Status_InvoiceDate', 0, 2, N'InvoiceDate', 2),
                (N'WarrantyClaim', N'UX_WarrantyClaim_OpenProductSerialId', 1, 1, N'ProductSerialId', 1),
                (N'WarrantyCoverage', N'AK_WarrantyCoverage_Id_ProductSerialId', 1, 1, N'Id', 2),
                (N'WarrantyCoverage', N'AK_WarrantyCoverage_Id_ProductSerialId', 1, 2, N'ProductSerialId', 2),
                (N'StockLedger', N'IX_StockLedger_Warehouse_Product_PostedAt', 0, 1, N'WarehouseId', 3),
                (N'StockLedger', N'IX_StockLedger_Warehouse_Product_PostedAt', 0, 2, N'ProductId', 3),
                (N'StockLedger', N'IX_StockLedger_Warehouse_Product_PostedAt', 0, 3, N'PostedAt', 3),
                (N'StockIn', N'IX_StockIn_StockCountSessionId', 0, 1, N'StockCountSessionId', 1),
                (N'StockIn', N'UX_StockIn_StockCountLineId', 1, 1, N'StockCountLineId', 1),
                (N'StockOut', N'IX_StockOut_StockCountSessionId', 0, 1, N'StockCountSessionId', 1),
                (N'StockOut', N'UX_StockOut_StockCountLineId', 1, 1, N'StockCountLineId', 1)
            ) AS expected(TableName, IndexName, IsUnique, KeyOrdinal, ColumnName, KeyCount)
            EXCEPT
            SELECT OBJECT_NAME(i.object_id), i.name, i.is_unique,
                   ic.key_ordinal, c.name,
                   (SELECT COUNT(*) FROM sys.index_columns AS keys
                    WHERE keys.object_id = i.object_id AND keys.index_id = i.index_id AND keys.key_ordinal > 0)
            FROM sys.indexes AS i
            INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.key_ordinal > 0
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.is_disabled = 0
        )        AND EXISTS
        (
            SELECT 1 FROM sys.indexes AS indexes
            WHERE indexes.object_id = OBJECT_ID(N'dbo.__WareProClientSession')
              AND indexes.name = N'IX___WareProClientSession_LastSeenUtc'
              AND indexes.is_disabled = 0
        )
        AND NOT EXISTS
        (
            SELECT expected.TableName, expected.IndexName
            FROM (VALUES
                (N'PurchaseInvoice', N'UX_PurchaseInvoice_StockInId'),
                (N'SalesInvoice', N'UX_SalesInvoice_StockOutId')
            ) AS expected(TableName, IndexName)
            EXCEPT
            SELECT OBJECT_NAME(indexes.object_id), indexes.name
            FROM sys.indexes AS indexes
            WHERE indexes.is_disabled = 0
              AND indexes.is_unique = 1
              AND indexes.has_filter = 1
        )
        AND OBJECT_ID(N'dbo.AllocateDocumentNumber', N'P') IS NOT NULL
        """;

    public static IReadOnlyList<string> BaselineBatches => SplitBatches(Sections.Value["baseline"])
        // helper tự tạo database qua master, nên baseline không được đổi catalog hoặc tạo lại database đích.
        .Where(batch => !batch.Contains("CREATE DATABASE", StringComparison.OrdinalIgnoreCase)
            && !batch.Contains("USE [ProductManagementDb]", StringComparison.OrdinalIgnoreCase))
        .ToArray();

    public static string BuildUpgradeSql(int expectedSchema, string appVersion)
    {
        ValidateRelease(expectedSchema, appVersion);
        var escapedVersion = appVersion.Replace("'", "''", StringComparison.Ordinal);
        var metadata = AsDynamicSql(SchemaMetadata);
        var versionStamp = AsDynamicSql($$"""
            UPDATE [dbo].[__WareProSchemaVersion]
            SET [Version] = {{expectedSchema}},
                [AppliedByAppVersion] = N'{{escapedVersion}}',
                [UpdatedAt] = SYSUTCDATETIME()
            WHERE [Id] = 1;
            """);
        // dynamic SQL giúp mỗi version biên dịch sau khi version trước tạo xong object, nhưng vẫn nằm trong cùng transaction.
        var version1 = AsDynamicSql(SchemaVersion1);
        var legacyShapeRepair = AsDynamicSql(LegacyShapeRepairSql);
        var version2 = AsDynamicSql(SchemaVersion2);
        var version3 = AsDynamicSql(SchemaVersion3);
        var version4 = AsDynamicSql(SchemaVersion4);
        var version5 = AsDynamicSql(SchemaVersion5);
        var version6 = AsDynamicSql(SchemaVersion6);
        var version7 = AsDynamicSql(SchemaVersion7);
        var version8 = AsDynamicSql(SchemaVersion8);
        var version9 = AsDynamicSql(SchemaVersion9);
        var version10 = AsDynamicSql(SchemaVersion10);
        var version11 = AsDynamicSql(SchemaVersion11);
        var archiveReplay = AsDynamicSql(SchemaArchiveReplay);
        var shapeValidation = AsDynamicSql($$"""
            IF NOT ({{ShapeValidationPredicate}})
                THROW 51028, 'WarePro schema shape validation failed.', 1;
            """);
        return $$"""
            {{metadata}}

            DECLARE @CurrentVersion INT = ISNULL(
                (SELECT TOP (1) [Version] FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1), 0);

            {{version1}}
            {{legacyShapeRepair}}

            {{version2}}
            IF @CurrentVersion < 3 BEGIN {{version3}} END;
            IF @CurrentVersion < 4 BEGIN {{version4}} END;
            IF @CurrentVersion < 5 BEGIN {{version5}} END;
            IF @CurrentVersion < 6 BEGIN {{version6}} END;
            IF @CurrentVersion < 7 BEGIN {{version7}} END;
            IF @CurrentVersion < 8 BEGIN {{version8}} END;

            IF @CurrentVersion < 9 BEGIN {{version9}} END;
            IF @CurrentVersion < 10 BEGIN {{version10}} END;
            IF @CurrentVersion < 11 BEGIN {{version11}} END;
            {{archiveReplay}}

            {{shapeValidation}}

            {{versionStamp}}
            """;
    }

    public static string BuildFinalizeSql(int expectedSchema, string minimumClientVersion)
    {
        ValidateRelease(expectedSchema, minimumClientVersion);
        var escapedVersion = minimumClientVersion.Replace("'", "''", StringComparison.Ordinal);
        // finalize gắn write gate lên danh sách bảng mutable; client phải khai báo schema không thấp hơn schema yêu cầu, maintenance được bỏ qua gate.
        return $$"""
            IF NOT EXISTS
            (
                SELECT 1 FROM dbo.__WareProSchemaVersion
                WHERE Id = 1 AND Version = {{expectedSchema}}
            )
                THROW 51028, 'WarePro schema is not ready for cutover.', 1;


            DECLARE @WareProTable sysname;
            DECLARE @WareProTriggerSql nvarchar(max);
            DECLARE WareProWriteGate CURSOR LOCAL FAST_FORWARD FOR
                SELECT [name]
                FROM (VALUES
                    (N'AppUser'), (N'AuditArchiveManifest'), (N'Brand'), (N'Category'),
                    (N'Customer'), (N'DocumentNumberCounter'), (N'Product'), (N'ProductSerial'), (N'ProductUnit'),
                    (N'PurchaseInvoice'), (N'PurchaseInvoiceLine'), (N'SalesInvoice'), (N'SalesInvoiceLine'),
                    (N'StockAdjustment'), (N'StockAdjustmentLine'), (N'StockBalance'),
                    (N'StockCountSession'), (N'StockCountLine'), (N'StockIn'), (N'StockInLine'),
                    (N'StockLedger'), (N'StockOut'), (N'StockOutLine'), (N'StockTransfer'),
                    (N'StockTransferLine'), (N'Supplier'), (N'Unit'), (N'Warehouse'),
                    (N'WarrantyClaim'), (N'WarrantyCoverage')
                ) AS MutableTables([name])
                WHERE OBJECT_ID(N'dbo.' + [name], N'U') IS NOT NULL;

            OPEN WareProWriteGate;
            FETCH NEXT FROM WareProWriteGate INTO @WareProTable;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SET @WareProTriggerSql =
                    N'CREATE OR ALTER TRIGGER dbo.' + QUOTENAME(N'TR_WareProClientGate_' + @WareProTable) +
                    N' ON dbo.' + QUOTENAME(@WareProTable) + N' AFTER INSERT, UPDATE, DELETE AS
                      BEGIN
                        SET NOCOUNT ON;
                        IF ISNULL(TRY_CONVERT(int, SESSION_CONTEXT(N''WareProClientSchema'')), 0) < {{expectedSchema}}
                           AND ISNULL(TRY_CONVERT(bit, SESSION_CONTEXT(N''WareProMaintenance'')), 0) = 0
                            THROW 51006, ''WarePro client version is not allowed to write to this database.'', 1;
                      END;';
                EXEC sys.sp_executesql @WareProTriggerSql;
                FETCH NEXT FROM WareProWriteGate INTO @WareProTable;
            END;
            CLOSE WareProWriteGate;
            DEALLOCATE WareProWriteGate;
            UPDATE dbo.__WareProSchemaVersion
            SET MinimumClientVersion = N'{{escapedVersion}}',
                AppliedByAppVersion = N'{{escapedVersion}}',
                UpdatedAt = SYSUTCDATETIME()
            WHERE Id = 1;

            """;
    }

    private static string AsDynamicSql(string sql) =>
        $"EXEC sys.sp_executesql N'{sql.Replace("'", "''", StringComparison.Ordinal)}';";

    private static void ValidateRelease(int expectedSchema, string version)
    {
        if (expectedSchema != 11)
            throw new ArgumentOutOfRangeException(nameof(expectedSchema));
        if (!Version.TryParse(version, out _))
            throw new ArgumentException("Version is invalid.", nameof(version));
    }

    private static string ReadEmbeddedText(string resourceName)
    {
        var assembly = typeof(DatabaseSchemaScripts).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Migration resource {resourceName} is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
    private static IReadOnlyDictionary<string, string> ReadSections()
    {
        var assembly = typeof(DatabaseSchemaScripts).Assembly;
        using var stream = assembly.GetManifestResourceStream("WarePro.Core.Resources.WarePro.Migrations.sql")
            ?? throw new InvalidOperationException("Migration bundle is missing.");
        using var reader = new StreamReader(stream);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string? name = null;
        var lines = new List<string>();
        foreach (var line in reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("-- ", StringComparison.Ordinal)
                && (line == "-- baseline" || line.EndsWith("Sql", StringComparison.Ordinal)))
            {
                if (name is not null) result[name] = string.Join(Environment.NewLine, lines).Trim();
                name = line[3..].Trim();
                lines.Clear();
            }
            else if (name is not null)
            {
                lines.Add(line);
            }
        }
        if (name is not null) result[name] = string.Join(Environment.NewLine, lines).Trim();
        return result;
    }

    private static string SingleBatch(string sectionName)
    {
        var batches = SplitBatches(Sections.Value[sectionName]);
        return batches.Count == 1
            ? batches[0]
            : throw new InvalidOperationException($"Migration section {sectionName} must contain one SQL batch.");
    }

    private static IReadOnlyList<string> SplitBatches(string sql)
    {
        var batches = new List<string>();
        var lines = new List<string>();
        using var reader = new StringReader(sql);
        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                AddBatch();
                continue;
            }
            lines.Add(line);
        }
        AddBatch();
        return batches;

        void AddBatch()
        {
            var batch = string.Join(Environment.NewLine, lines).Trim();
            if (batch.Length > 0) batches.Add(batch);
            lines.Clear();
        }
    }
}
