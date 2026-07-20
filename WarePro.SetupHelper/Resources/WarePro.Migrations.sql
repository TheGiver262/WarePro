-- baseline
-- Baseline SQL Server schema for the product and warranty management system.
-- baseline tạo schema mới; các khối version cuối file áp dụng thay đổi tăng dần và phải chạy lặp an toàn.
-- This schema follows the current design baseline:
-- 1. Future-ready Warehouse table, while phase 1 uses one hidden default warehouse.
-- 2. Opening stock import is represented by StockIn with PurposeCode = 'OpeningBalance'.
-- 3. Basic tax is stored directly on invoice headers and invoice lines.

IF DB_ID(N'ProductManagementDb') IS NULL
BEGIN
    CREATE DATABASE [ProductManagementDb];
END
GO

USE [ProductManagementDb];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE TABLE dbo.AppUser
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Username              NVARCHAR(100) NOT NULL,
    PasswordHash          NVARCHAR(255) NOT NULL,
    FullName              NVARCHAR(200) NOT NULL,
    RoleCode              NVARCHAR(50) NOT NULL,
    MustChangePassword    BIT NOT NULL CONSTRAINT DF_AppUser_MustChangePassword DEFAULT (1),
    FailedLoginCount      INT NOT NULL CONSTRAINT DF_AppUser_FailedLoginCount DEFAULT (0),
    CreatedBy             INT NULL,
    CreatedAt             DATETIME2(0) NOT NULL CONSTRAINT DF_AppUser_CreatedAt DEFAULT (SYSUTCDATETIME()),
    LockoutUntil          DATETIME2(0) NULL,
    LastFailedLoginAt     DATETIME2(0) NULL,
    LastPasswordChangedAt DATETIME2(0) NULL,
    LastLoginAt           DATETIME2(0) NULL,
    IsActive              BIT NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT (1),
    CONSTRAINT FK_AppUser_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE UNIQUE INDEX UX_AppUser_Username ON dbo.AppUser(Username);
CREATE INDEX IX_AppUser_CreatedBy ON dbo.AppUser(CreatedBy);
GO

CREATE TABLE dbo.Category
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CategoryCode NVARCHAR(50) NOT NULL,
    DisplayName  NVARCHAR(200) NOT NULL,
    IsActive     BIT NOT NULL CONSTRAINT DF_Category_IsActive DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Category_CategoryCode ON dbo.Category(CategoryCode);
GO

CREATE TABLE dbo.Brand
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    BrandCode     NVARCHAR(50) NOT NULL,
    DisplayName   NVARCHAR(200) NOT NULL,
    OriginCountry NVARCHAR(100) NULL,
    IsActive      BIT NOT NULL CONSTRAINT DF_Brand_IsActive DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Brand_BrandCode ON dbo.Brand(BrandCode);
GO

CREATE TABLE dbo.Unit
(
    Id          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UnitCode    NVARCHAR(50) NOT NULL,
    DisplayName NVARCHAR(100) NOT NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_Unit_IsActive DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Unit_UnitCode ON dbo.Unit(UnitCode);
GO

CREATE TABLE dbo.Supplier
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SupplierCode NVARCHAR(50) NOT NULL,
    DisplayName  NVARCHAR(200) NOT NULL,
    Phone        NVARCHAR(30) NULL,
    Email        NVARCHAR(255) NULL,
    Address      NVARCHAR(500) NULL,
    IsActive     BIT NOT NULL CONSTRAINT DF_Supplier_IsActive DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Supplier_SupplierCode ON dbo.Supplier(SupplierCode);
GO

CREATE TABLE dbo.Customer
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerCode NVARCHAR(50) NOT NULL,
    DisplayName  NVARCHAR(200) NOT NULL,
    Phone        NVARCHAR(30) NULL,
    Email        NVARCHAR(255) NULL,
    Address      NVARCHAR(500) NULL,
    IsActive     BIT NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Customer_CustomerCode ON dbo.Customer(CustomerCode);
GO

CREATE TABLE dbo.Warehouse
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    WarehouseCode NVARCHAR(50) NOT NULL,
    DisplayName   NVARCHAR(200) NOT NULL,
    IsDefault     BIT NOT NULL CONSTRAINT DF_Warehouse_IsDefault DEFAULT (0),
    IsActive      BIT NOT NULL CONSTRAINT DF_Warehouse_IsActive DEFAULT (1)
);
GO

CREATE UNIQUE INDEX UX_Warehouse_WarehouseCode ON dbo.Warehouse(WarehouseCode);
CREATE UNIQUE INDEX UX_Warehouse_SingleDefault ON dbo.Warehouse(IsDefault) WHERE IsDefault = 1;
GO

CREATE TABLE dbo.Product
(
    Id                   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProductCode          NVARCHAR(50) NOT NULL,
    DisplayName          NVARCHAR(200) NOT NULL,
    CategoryId           INT NOT NULL,
    BrandId              INT NOT NULL,
    DefaultUnitId        INT NOT NULL,
    DefaultPrice         DECIMAL(18,2) NOT NULL CONSTRAINT DF_Product_DefaultPrice DEFAULT (0),
    OriginCountry        NVARCHAR(100) NULL,
    WarrantyPeriodMonths INT NOT NULL CONSTRAINT DF_Product_WarrantyPeriodMonths DEFAULT (0),
    IsSerialTracked      BIT NOT NULL CONSTRAINT DF_Product_IsSerialTracked DEFAULT (0),
    IsActive             BIT NOT NULL CONSTRAINT DF_Product_IsActive DEFAULT (1),
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category(Id),
    CONSTRAINT FK_Product_Brand FOREIGN KEY (BrandId) REFERENCES dbo.Brand(Id),
    CONSTRAINT FK_Product_DefaultUnit FOREIGN KEY (DefaultUnitId) REFERENCES dbo.Unit(Id),
    CONSTRAINT CK_Product_DefaultPrice_NonNegative CHECK (DefaultPrice >= 0),
    CONSTRAINT CK_Product_WarrantyMonths_NonNegative CHECK (WarrantyPeriodMonths >= 0)
);
GO

CREATE UNIQUE INDEX UX_Product_ProductCode ON dbo.Product(ProductCode);
GO

CREATE TABLE dbo.ProductUnit
(
    Id               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProductId        INT NOT NULL,
    UnitId           INT NOT NULL,
    ConversionFactor DECIMAL(18,6) NOT NULL,
    IsBaseUnit       BIT NOT NULL CONSTRAINT DF_ProductUnit_IsBaseUnit DEFAULT (0),
    IsPurchaseUnit   BIT NOT NULL CONSTRAINT DF_ProductUnit_IsPurchaseUnit DEFAULT (0),
    IsSalesUnit      BIT NOT NULL CONSTRAINT DF_ProductUnit_IsSalesUnit DEFAULT (0),
    CONSTRAINT FK_ProductUnit_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_ProductUnit_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id),
    CONSTRAINT CK_ProductUnit_ConversionFactor_Positive CHECK (ConversionFactor > 0)
);
GO

CREATE UNIQUE INDEX UX_ProductUnit_Product_Unit ON dbo.ProductUnit(ProductId, UnitId);
CREATE UNIQUE INDEX UX_ProductUnit_BaseUnit ON dbo.ProductUnit(ProductId) WHERE IsBaseUnit = 1;
GO

CREATE TABLE dbo.StockBalance
(
    Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    WarehouseId       INT NOT NULL,
    ProductId         INT NOT NULL,
    OnHandQuantity    DECIMAL(18,2) NOT NULL CONSTRAINT DF_StockBalance_OnHand DEFAULT (0),
    AvailableQuantity DECIMAL(18,2) NOT NULL CONSTRAINT DF_StockBalance_Available DEFAULT (0),
    ReservedQuantity  DECIMAL(18,2) NOT NULL CONSTRAINT DF_StockBalance_Reserved DEFAULT (0),
    CONSTRAINT FK_StockBalance_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockBalance_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT CK_StockBalance_OnHand_NonNegative CHECK (OnHandQuantity >= 0),
    CONSTRAINT CK_StockBalance_Available_NonNegative CHECK (AvailableQuantity >= 0),
    CONSTRAINT CK_StockBalance_Reserved_NonNegative CHECK (ReservedQuantity >= 0)
);
GO

CREATE UNIQUE INDEX UX_StockBalance_Warehouse_Product ON dbo.StockBalance(WarehouseId, ProductId);
GO

CREATE TABLE dbo.StockIn
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DocumentCode NVARCHAR(50) NOT NULL,
    SupplierId   INT NULL,
    WarehouseId  INT NOT NULL,
    PurposeCode  NVARCHAR(50) NOT NULL,
    Status       NVARCHAR(50) NOT NULL,
    CreatedBy    INT NOT NULL,
    ApprovedBy   INT NULL,
    PostedBy     INT NULL,
    CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_StockIn_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ApprovedAt   DATETIME2(0) NULL,
    PostedAt     DATETIME2(0) NULL,
    CONSTRAINT FK_StockIn_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Supplier(Id),
    CONSTRAINT FK_StockIn_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockIn_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockIn_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockIn_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT CK_StockIn_PurposeCode CHECK (PurposeCode IN (N'Purchase', N'OpeningBalance'))
);
GO

CREATE UNIQUE INDEX UX_StockIn_DocumentCode ON dbo.StockIn(DocumentCode);
GO

CREATE TABLE dbo.StockInLine
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    StockInId    INT NOT NULL,
    ProductId    INT NOT NULL,
    UnitId       INT NOT NULL,
    Quantity     DECIMAL(18,2) NOT NULL,
    BaseQuantity DECIMAL(18,2) NOT NULL,
    UnitPrice    DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_StockInLine_StockIn FOREIGN KEY (StockInId) REFERENCES dbo.StockIn(Id),
    CONSTRAINT FK_StockInLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_StockInLine_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id),
    CONSTRAINT CK_StockInLine_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_StockInLine_BaseQuantity_Positive CHECK (BaseQuantity > 0),
    CONSTRAINT CK_StockInLine_UnitPrice_NonNegative CHECK (UnitPrice >= 0)
);
GO

CREATE TABLE dbo.StockOut
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DocumentCode NVARCHAR(50) NOT NULL,
    CustomerId   INT NOT NULL,
    WarehouseId  INT NOT NULL,
    PurposeCode  NVARCHAR(50) NOT NULL,
    Status       NVARCHAR(50) NOT NULL,
    CreatedBy    INT NOT NULL,
    ApprovedBy   INT NULL,
    PostedBy     INT NULL,
    CreatedAt    DATETIME2(0) NOT NULL CONSTRAINT DF_StockOut_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ApprovedAt   DATETIME2(0) NULL,
    PostedAt     DATETIME2(0) NULL,
    CONSTRAINT FK_StockOut_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(Id),
    CONSTRAINT FK_StockOut_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockOut_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockOut_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockOut_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT CK_StockOut_PurposeCode CHECK (PurposeCode IN (N'Sale', N'WarrantyReplacement'))
);
GO

CREATE UNIQUE INDEX UX_StockOut_DocumentCode ON dbo.StockOut(DocumentCode);
GO

CREATE TABLE dbo.StockOutLine
(
    Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    StockOutId   INT NOT NULL,
    ProductId    INT NOT NULL,
    UnitId       INT NOT NULL,
    Quantity     DECIMAL(18,2) NOT NULL,
    BaseQuantity DECIMAL(18,2) NOT NULL,
    UnitPrice    DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_StockOutLine_StockOut FOREIGN KEY (StockOutId) REFERENCES dbo.StockOut(Id),
    CONSTRAINT FK_StockOutLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_StockOutLine_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id),
    CONSTRAINT CK_StockOutLine_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_StockOutLine_BaseQuantity_Positive CHECK (BaseQuantity > 0),
    CONSTRAINT CK_StockOutLine_UnitPrice_NonNegative CHECK (UnitPrice >= 0)
);
GO

CREATE TABLE dbo.StockCountSession
(
    Id          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SessionCode NVARCHAR(50) NOT NULL,
    WarehouseId INT NOT NULL,
    Status      NVARCHAR(50) NOT NULL,
    CreatedBy   INT NOT NULL,
    ApprovedBy  INT NULL,
    PostedBy    INT NULL,
    CountDate   DATETIME2(0) NOT NULL,
    ApprovedAt  DATETIME2(0) NULL,
    PostedAt    DATETIME2(0) NULL,
    CONSTRAINT FK_StockCountSession_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockCountSession_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockCountSession_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockCountSession_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE UNIQUE INDEX UX_StockCountSession_SessionCode ON dbo.StockCountSession(SessionCode);
GO

CREATE TABLE dbo.StockCountLine
(
    Id               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SessionId        INT NOT NULL,
    ProductId        INT NOT NULL,
    SystemQuantity   DECIMAL(18,2) NOT NULL,
    CountedQuantity  DECIMAL(18,2) NOT NULL,
    VarianceQuantity DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_StockCountLine_Session FOREIGN KEY (SessionId) REFERENCES dbo.StockCountSession(Id),
    CONSTRAINT FK_StockCountLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id)
);
GO

CREATE TABLE dbo.StockAdjustment
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    DocumentCode          NVARCHAR(50) NOT NULL,
    WarehouseId           INT NOT NULL,
    AdjustmentType        NVARCHAR(50) NOT NULL,
    Status                NVARCHAR(50) NOT NULL,
    ReferenceDocumentType NVARCHAR(50) NULL,
    ReferenceDocumentId   INT NULL,
    ReasonCode            NVARCHAR(100) NOT NULL,
    CreatedBy             INT NOT NULL,
    ApprovedBy            INT NULL,
    PostedBy              INT NULL,
    ApprovedAt            DATETIME2(0) NULL,
    PostedAt              DATETIME2(0) NULL,
    CONSTRAINT FK_StockAdjustment_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockAdjustment_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockAdjustment_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockAdjustment_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE UNIQUE INDEX UX_StockAdjustment_DocumentCode ON dbo.StockAdjustment(DocumentCode);
GO

CREATE TABLE dbo.StockAdjustmentLine
(
    Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AdjustmentId      INT NOT NULL,
    ProductId         INT NOT NULL,
    ProductSerialId   INT NULL,
    QuantityDelta     DECIMAL(18,2) NOT NULL,
    BaseQuantityDelta DECIMAL(18,2) NOT NULL,
    Direction         NVARCHAR(20) NOT NULL,
    CONSTRAINT FK_StockAdjustmentLine_Adjustment FOREIGN KEY (AdjustmentId) REFERENCES dbo.StockAdjustment(Id),
    CONSTRAINT FK_StockAdjustmentLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT CK_StockAdjustmentLine_Direction CHECK (Direction IN (N'In', N'Out'))
);
GO

CREATE TABLE dbo.PurchaseInvoice
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    InvoiceCode   NVARCHAR(50) NOT NULL,
    SupplierId    INT NOT NULL,
    StockInId     INT NULL,
    InvoiceDate   DATETIME2(0) NOT NULL,
    SubTotal      DECIMAL(18,2) NOT NULL,
    TaxAmount     DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseInvoice_TaxAmount DEFAULT (0),
    GrandTotal    DECIMAL(18,2) NOT NULL,
    Notes         NVARCHAR(MAX) NULL,
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_PurchaseInvoice_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_PurchaseInvoice_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Supplier(Id),
    CONSTRAINT FK_PurchaseInvoice_StockIn FOREIGN KEY (StockInId) REFERENCES dbo.StockIn(Id),
    CONSTRAINT CK_PurchaseInvoice_SubTotal_NonNegative CHECK (SubTotal >= 0),
    CONSTRAINT CK_PurchaseInvoice_TaxAmount_NonNegative CHECK (TaxAmount >= 0),
    CONSTRAINT CK_PurchaseInvoice_GrandTotal_NonNegative CHECK (GrandTotal >= 0)
);
GO

CREATE UNIQUE INDEX UX_PurchaseInvoice_InvoiceCode ON dbo.PurchaseInvoice(InvoiceCode);
GO

CREATE TABLE dbo.PurchaseInvoiceLine
(
    Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PurchaseInvoiceId INT NOT NULL,
    ProductId         INT NOT NULL,
    UnitId            INT NOT NULL,
    StockInLineId     INT NULL,
    Quantity          DECIMAL(18,2) NOT NULL,
    UnitPrice         DECIMAL(18,2) NOT NULL,
    SubTotal          DECIMAL(18,2) NOT NULL,
    TaxRate           DECIMAL(9,4) NOT NULL CONSTRAINT DF_PurchaseInvoiceLine_TaxRate DEFAULT (0),
    TaxAmount         DECIMAL(18,2) NOT NULL CONSTRAINT DF_PurchaseInvoiceLine_TaxAmount DEFAULT (0),
    GrandTotal        DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_PurchaseInvoiceLine_Invoice FOREIGN KEY (PurchaseInvoiceId) REFERENCES dbo.PurchaseInvoice(Id),
    CONSTRAINT FK_PurchaseInvoiceLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_PurchaseInvoiceLine_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id),
    CONSTRAINT FK_PurchaseInvoiceLine_StockInLine FOREIGN KEY (StockInLineId) REFERENCES dbo.StockInLine(Id),
    CONSTRAINT CK_PurchaseInvoiceLine_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_PurchaseInvoiceLine_UnitPrice_NonNegative CHECK (UnitPrice >= 0),
    CONSTRAINT CK_PurchaseInvoiceLine_SubTotal_NonNegative CHECK (SubTotal >= 0),
    CONSTRAINT CK_PurchaseInvoiceLine_TaxRate_NonNegative CHECK (TaxRate >= 0),
    CONSTRAINT CK_PurchaseInvoiceLine_TaxAmount_NonNegative CHECK (TaxAmount >= 0),
    CONSTRAINT CK_PurchaseInvoiceLine_GrandTotal_NonNegative CHECK (GrandTotal >= 0)
);
GO

CREATE TABLE dbo.SalesInvoice
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    InvoiceCode   NVARCHAR(50) NOT NULL,
    CustomerId    INT NOT NULL,
    StockOutId    INT NULL,
    InvoiceDate   DATETIME2(0) NOT NULL,
    SubTotal      DECIMAL(18,2) NOT NULL,
    TaxAmount     DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesInvoice_TaxAmount DEFAULT (0),
    GrandTotal    DECIMAL(18,2) NOT NULL,
    Notes         NVARCHAR(MAX) NULL,
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_SalesInvoice_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_SalesInvoice_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(Id),
    CONSTRAINT FK_SalesInvoice_StockOut FOREIGN KEY (StockOutId) REFERENCES dbo.StockOut(Id),
    CONSTRAINT CK_SalesInvoice_SubTotal_NonNegative CHECK (SubTotal >= 0),
    CONSTRAINT CK_SalesInvoice_TaxAmount_NonNegative CHECK (TaxAmount >= 0),
    CONSTRAINT CK_SalesInvoice_GrandTotal_NonNegative CHECK (GrandTotal >= 0)
);
GO

CREATE UNIQUE INDEX UX_SalesInvoice_InvoiceCode ON dbo.SalesInvoice(InvoiceCode);
GO

CREATE TABLE dbo.SalesInvoiceLine
(
    Id             INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SalesInvoiceId INT NOT NULL,
    ProductId      INT NOT NULL,
    UnitId         INT NOT NULL,
    StockOutLineId INT NULL,
    Quantity       DECIMAL(18,2) NOT NULL,
    UnitPrice      DECIMAL(18,2) NOT NULL,
    SubTotal       DECIMAL(18,2) NOT NULL,
    TaxRate        DECIMAL(9,4) NOT NULL CONSTRAINT DF_SalesInvoiceLine_TaxRate DEFAULT (0),
    TaxAmount      DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesInvoiceLine_TaxAmount DEFAULT (0),
    GrandTotal     DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_SalesInvoiceLine_Invoice FOREIGN KEY (SalesInvoiceId) REFERENCES dbo.SalesInvoice(Id),
    CONSTRAINT FK_SalesInvoiceLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_SalesInvoiceLine_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id),
    CONSTRAINT FK_SalesInvoiceLine_StockOutLine FOREIGN KEY (StockOutLineId) REFERENCES dbo.StockOutLine(Id),
    CONSTRAINT CK_SalesInvoiceLine_Quantity_Positive CHECK (Quantity > 0),
    CONSTRAINT CK_SalesInvoiceLine_UnitPrice_NonNegative CHECK (UnitPrice >= 0),
    CONSTRAINT CK_SalesInvoiceLine_SubTotal_NonNegative CHECK (SubTotal >= 0),
    CONSTRAINT CK_SalesInvoiceLine_TaxRate_NonNegative CHECK (TaxRate >= 0),
    CONSTRAINT CK_SalesInvoiceLine_TaxAmount_NonNegative CHECK (TaxAmount >= 0),
    CONSTRAINT CK_SalesInvoiceLine_GrandTotal_NonNegative CHECK (GrandTotal >= 0)
);
GO

CREATE TABLE dbo.ProductSerial
(
    Id                 INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProductId          INT NOT NULL,
    SerialNumber       NVARCHAR(150) NOT NULL,
    CurrentStatus      NVARCHAR(50) NOT NULL,
    CurrentWarehouseId INT NULL,
    LastStockInLineId  INT NOT NULL,
    LastStockOutLineId INT NULL,
    CONSTRAINT FK_ProductSerial_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_ProductSerial_CurrentWarehouse FOREIGN KEY (CurrentWarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_ProductSerial_LastStockInLine FOREIGN KEY (LastStockInLineId) REFERENCES dbo.StockInLine(Id),
    CONSTRAINT FK_ProductSerial_LastStockOutLine FOREIGN KEY (LastStockOutLineId) REFERENCES dbo.StockOutLine(Id)
);
GO

CREATE UNIQUE INDEX UX_ProductSerial_SerialNumber ON dbo.ProductSerial(SerialNumber);
GO

ALTER TABLE dbo.StockAdjustmentLine
ADD CONSTRAINT FK_StockAdjustmentLine_ProductSerial
    FOREIGN KEY (ProductSerialId) REFERENCES dbo.ProductSerial(Id);
GO

CREATE TABLE dbo.StockLedger
(
    Id                 INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    WarehouseId        INT NOT NULL,
    ProductId          INT NOT NULL,
    ProductSerialId    INT NULL,
    SourceDocumentType NVARCHAR(50) NOT NULL,
    SourceDocumentId   INT NOT NULL,
    MovementType       NVARCHAR(50) NOT NULL,
    Quantity           DECIMAL(18,2) NOT NULL,
    PostedBy           INT NOT NULL,
    PostedAt           DATETIME2(0) NOT NULL CONSTRAINT DF_StockLedger_PostedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_StockLedger_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockLedger_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_StockLedger_ProductSerial FOREIGN KEY (ProductSerialId) REFERENCES dbo.ProductSerial(Id),
    CONSTRAINT FK_StockLedger_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE INDEX IX_StockLedger_Warehouse_Product_PostedAt ON dbo.StockLedger(WarehouseId, ProductId, PostedAt);
CREATE INDEX IX_StockLedger_SourceDocument ON dbo.StockLedger(SourceDocumentType, SourceDocumentId);
GO

CREATE TABLE dbo.WarrantyCoverage
(
    Id                INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProductSerialId   INT NOT NULL,
    CustomerId        INT NOT NULL,
    SalesInvoiceId    INT NULL,
    WarrantyStartDate DATETIME2(0) NOT NULL,
    WarrantyEndDate   DATETIME2(0) NOT NULL,
    CoverageStatus    NVARCHAR(50) NOT NULL,
    CONSTRAINT FK_WarrantyCoverage_ProductSerial FOREIGN KEY (ProductSerialId) REFERENCES dbo.ProductSerial(Id),
    CONSTRAINT FK_WarrantyCoverage_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer(Id),
    CONSTRAINT FK_WarrantyCoverage_SalesInvoice FOREIGN KEY (SalesInvoiceId) REFERENCES dbo.SalesInvoice(Id),
    CONSTRAINT CK_WarrantyCoverage_DateRange CHECK (WarrantyEndDate >= WarrantyStartDate)
);
GO

CREATE UNIQUE INDEX UX_WarrantyCoverage_Active_PerSerial
ON dbo.WarrantyCoverage(ProductSerialId)
WHERE CoverageStatus = N'Active';
GO

CREATE TABLE dbo.WarrantyClaim
(
    Id                    INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ClaimCode             NVARCHAR(50) NOT NULL,
    WarrantyCoverageId    INT NOT NULL,
    ProductSerialId       INT NOT NULL,
    ReplacementSerialId   INT NULL,
    ReplacementStockOutId INT NULL,
    ReceivedDate          DATETIME2(0) NOT NULL,
    ProblemDescription    NVARCHAR(1000) NULL,
    TechnicalConclusion   NVARCHAR(1000) NULL,
    ManufacturerResult    NVARCHAR(1000) NULL,
    RejectionReason       NVARCHAR(1000) NULL,
    ProcessingNote        NVARCHAR(1000) NULL,
    ResolutionType        NVARCHAR(50) NULL,
    Status                NVARCHAR(50) NOT NULL,
    ApprovedBy            INT NULL,
    ProcessedBy           INT NOT NULL,
    ClosedDate            DATETIME2(0) NULL,
    CONSTRAINT FK_WarrantyClaim_Coverage FOREIGN KEY (WarrantyCoverageId) REFERENCES dbo.WarrantyCoverage(Id),
    CONSTRAINT FK_WarrantyClaim_ProductSerial FOREIGN KEY (ProductSerialId) REFERENCES dbo.ProductSerial(Id),
    CONSTRAINT FK_WarrantyClaim_ReplacementSerial FOREIGN KEY (ReplacementSerialId) REFERENCES dbo.ProductSerial(Id),
    CONSTRAINT FK_WarrantyClaim_ReplacementStockOut FOREIGN KEY (ReplacementStockOutId) REFERENCES dbo.StockOut(Id),
    CONSTRAINT FK_WarrantyClaim_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_WarrantyClaim_ProcessedBy FOREIGN KEY (ProcessedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE UNIQUE INDEX UX_WarrantyClaim_ClaimCode ON dbo.WarrantyClaim(ClaimCode);
CREATE UNIQUE INDEX UX_WarrantyClaim_OpenClaim_PerSerial
ON dbo.WarrantyClaim(ProductSerialId)
WHERE Status <> N'Closed';
GO

CREATE TABLE dbo.AuditLog
(
    Id          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EntityName  NVARCHAR(100) NOT NULL,
    EntityId    INT NOT NULL,
    ActionCode  NVARCHAR(50) NOT NULL,
    BeforeJson  NVARCHAR(MAX) NULL,
    AfterJson   NVARCHAR(MAX) NULL,
    PerformedBy INT NOT NULL,
    PerformedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLog_PerformedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_AuditLog_PerformedBy FOREIGN KEY (PerformedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog(EntityName, EntityId, PerformedAt);
GO

CREATE TABLE dbo.AuditArchiveManifest
(
    Id            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    OperationId   UNIQUEIDENTIFIER NOT NULL,
    ActorId       INT NOT NULL,
    RangeStartUtc DATETIME2(0) NOT NULL,
    RangeEndUtc   DATETIME2(0) NOT NULL,
    [RowCount]    INT NOT NULL,
    FileName      NVARCHAR(260) NOT NULL,
    Sha256Hash    NCHAR(64) NOT NULL,
    CreatedAtUtc  DATETIME2(0) NOT NULL CONSTRAINT DF_AuditArchiveManifest_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_AuditArchiveManifest_Actor FOREIGN KEY (ActorId) REFERENCES dbo.AppUser(Id)
);
GO

CREATE UNIQUE INDEX UX_AuditArchiveManifest_OperationId ON dbo.AuditArchiveManifest(OperationId);
CREATE INDEX IX_AuditArchiveManifest_CreatedAtUtc ON dbo.AuditArchiveManifest(CreatedAtUtc);
GO

CREATE TABLE dbo.StockTransfer
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockTransfer PRIMARY KEY,
    DocumentCode    NVARCHAR(50) NOT NULL,
    FromWarehouseId INT NOT NULL,
    ToWarehouseId   INT NOT NULL,
    Status          NVARCHAR(50) NOT NULL,
    TransferDate    DATETIME2(0) NOT NULL,
    Notes           NVARCHAR(500) NULL,
    CreatedBy       INT NOT NULL,
    ApprovedBy      INT NULL,
    PostedBy        INT NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_StockTransfer_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ApprovedAt      DATETIME2(0) NULL,
    PostedAt        DATETIME2(0) NULL,
    UpdatedAt       DATETIME2(0) NULL,
    UpdatedBy       INT NULL,
    CONSTRAINT FK_StockTransfer_FromWarehouse FOREIGN KEY (FromWarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockTransfer_ToWarehouse FOREIGN KEY (ToWarehouseId) REFERENCES dbo.Warehouse(Id),
    CONSTRAINT FK_StockTransfer_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockTransfer_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES dbo.AppUser(Id),
    CONSTRAINT FK_StockTransfer_PostedBy FOREIGN KEY (PostedBy) REFERENCES dbo.AppUser(Id)
);
GO

CREATE UNIQUE INDEX UX_StockTransfer_DocumentCode ON dbo.StockTransfer(DocumentCode);
GO

CREATE TABLE dbo.StockTransferLine
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_StockTransferLine PRIMARY KEY,
    StockTransferId INT NOT NULL,
    ProductId       INT NOT NULL,
    UnitId          INT NOT NULL,
    Quantity        DECIMAL(18,2) NOT NULL,
    BaseQuantity    DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_StockTransferLine_StockTransfer FOREIGN KEY (StockTransferId) REFERENCES dbo.StockTransfer(Id),
    CONSTRAINT FK_StockTransferLine_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id),
    CONSTRAINT FK_StockTransferLine_Unit FOREIGN KEY (UnitId) REFERENCES dbo.Unit(Id)
);
GO

CREATE INDEX IX_StockTransferLine_StockTransferId ON dbo.StockTransferLine(StockTransferId);
CREATE INDEX IX_StockTransferLine_ProductId ON dbo.StockTransferLine(ProductId);
CREATE INDEX IX_StockTransferLine_UnitId ON dbo.StockTransferLine(UnitId);
GO
-- Default hidden warehouse for phase 1.
IF NOT EXISTS (SELECT 1 FROM dbo.Warehouse WHERE IsDefault = 1)
BEGIN
    INSERT INTO dbo.Warehouse (WarehouseCode, DisplayName, IsDefault, IsActive)
    VALUES (N'DEFAULT', N'Kho mặc định', 1, 1);
END
GO

-- Useful supporting indexes for common lookups.
CREATE INDEX IX_Product_CategoryId ON dbo.Product(CategoryId);
CREATE INDEX IX_Product_BrandId ON dbo.Product(BrandId);
CREATE INDEX IX_StockIn_Warehouse_ProductLookup ON dbo.StockIn(WarehouseId, PostedAt);
CREATE INDEX IX_StockOut_Warehouse_ProductLookup ON dbo.StockOut(WarehouseId, PostedAt);
CREATE INDEX IX_StockIn_SupplierId ON dbo.StockIn(SupplierId);
CREATE INDEX IX_StockOut_CustomerId ON dbo.StockOut(CustomerId);
CREATE INDEX IX_WarrantyClaim_CoverageId ON dbo.WarrantyClaim(WarrantyCoverageId);
CREATE INDEX IX_WarrantyCoverage_CustomerId ON dbo.WarrantyCoverage(CustomerId);
GO

-- Notes:
-- 1. StockLedger.SourceDocumentType + SourceDocumentId is polymorphic, so no FK is created here.
-- 2. StockAdjustment.ReferenceDocumentType + ReferenceDocumentId is also polymorphic.
-- 3. Opening stock Excel/CSV import creates StockIn with PurposeCode = 'OpeningBalance'.
-- 4. StockBalance is unique by WarehouseId + ProductId to support future multi-warehouse expansion.
-- 5. Tax support is intentionally basic: SubTotal, TaxRate, TaxAmount, GrandTotal only.
-- 6. Account create/reset/role changes must write AuditLog; never store plaintext passwords.

GO
-- SchemaMetadataSql
-- metadata là mốc để setup helper chọn đúng các migration còn thiếu cho từng database.

        IF OBJECT_ID(N'[dbo].[__WareProSchemaVersion]', N'U') IS NULL
            EXEC sys.sp_executesql N'
                CREATE TABLE [dbo].[__WareProSchemaVersion]
                (
                    [Id] INT NOT NULL CONSTRAINT [PK___WareProSchemaVersion] PRIMARY KEY,
                    [Version] INT NOT NULL,
                    [MinimumClientVersion] NVARCHAR(32) NOT NULL,
                    [AppliedByAppVersion] NVARCHAR(64) NOT NULL,
                    [UpdatedAt] DATETIME2 NOT NULL
                );';

        IF COL_LENGTH('__WareProSchemaVersion', 'MinimumClientVersion') IS NULL
            EXEC sys.sp_executesql N'ALTER TABLE [dbo].[__WareProSchemaVersion] ADD [MinimumClientVersion] NVARCHAR(32) NULL;';

        IF COL_LENGTH('__WareProSchemaVersion', 'AppliedByAppVersion') IS NULL
            EXEC sys.sp_executesql N'ALTER TABLE [dbo].[__WareProSchemaVersion] ADD [AppliedByAppVersion] NVARCHAR(64) NULL;';

        EXEC sys.sp_executesql N'
            UPDATE [dbo].[__WareProSchemaVersion]
            SET [MinimumClientVersion] = COALESCE(NULLIF([MinimumClientVersion], N''''), N''1.0.0''),
                [AppliedByAppVersion] = COALESCE(NULLIF([AppliedByAppVersion], N''''), N''1.0.0'');';

        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[__WareProSchemaVersion]
                ALTER COLUMN [MinimumClientVersion] NVARCHAR(32) NOT NULL;';

        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[__WareProSchemaVersion]
                ALTER COLUMN [AppliedByAppVersion] NVARCHAR(64) NOT NULL;';
        IF NOT EXISTS (SELECT 1 FROM [dbo].[__WareProSchemaVersion] WHERE [Id] = 1)
            EXEC sys.sp_executesql N'
                INSERT INTO [dbo].[__WareProSchemaVersion]
                    ([Id], [Version], [MinimumClientVersion], [AppliedByAppVersion], [UpdatedAt])
                VALUES (1, 0, N''1.0.0'', N''1.0.0'', SYSUTCDATETIME());';
GO
-- SchemaVersion1Sql

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
        
GO
-- SchemaVersion2Sql

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
        
GO
-- SchemaVersion3Sql

        IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_WarrantyClaim_OpenClaim_PerSerial' AND object_id = OBJECT_ID('WarrantyClaim'))
            DROP INDEX UX_WarrantyClaim_OpenClaim_PerSerial ON WarrantyClaim;
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WarrantyClaim_ProductSerialId' AND object_id = OBJECT_ID('WarrantyClaim'))
            CREATE INDEX IX_WarrantyClaim_ProductSerialId ON WarrantyClaim (ProductSerialId);
        
GO
-- SchemaVersion4Sql

        IF COL_LENGTH('StockIn', 'ApprovedAt') IS NULL ALTER TABLE StockIn ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockIn', 'PostedAt') IS NULL ALTER TABLE StockIn ADD PostedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockOut', 'ApprovedAt') IS NULL ALTER TABLE StockOut ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockOut', 'PostedAt') IS NULL ALTER TABLE StockOut ADD PostedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockAdjustment', 'ApprovedAt') IS NULL ALTER TABLE StockAdjustment ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockAdjustment', 'PostedAt') IS NULL ALTER TABLE StockAdjustment ADD PostedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockTransfer', 'ApprovedAt') IS NULL ALTER TABLE StockTransfer ADD ApprovedAt DATETIME2(0) NULL;
        IF COL_LENGTH('StockTransfer', 'PostedAt') IS NULL ALTER TABLE StockTransfer ADD PostedAt DATETIME2(0) NULL;
        
GO
-- SchemaVersion5Sql
-- chuẩn hóa dữ liệu cũ trước khi thêm constraint để bản nâng cấp không kẹt ở giá trị legacy.

        IF OBJECT_ID(N'[dbo].[SalesInvoice]', N'U') IS NOT NULL
        BEGIN
            UPDATE SalesInvoice SET PaymentStatus = 'Unpaid' WHERE UPPER(PaymentStatus) = 'UNPAID';
            UPDATE SalesInvoice SET PaymentStatus = 'PartiallyPaid' WHERE UPPER(PaymentStatus) IN ('PARTIAL', 'PARTIALLYPAID');
            UPDATE SalesInvoice SET PaymentStatus = 'Paid' WHERE UPPER(PaymentStatus) = 'PAID';
            UPDATE SalesInvoice SET PaymentStatus = 'Overdue' WHERE UPPER(PaymentStatus) = 'OVERDUE';
            UPDATE SalesInvoice
            SET PaymentStatus = CASE
                WHEN PaidAmount >= GrandTotal AND GrandTotal > 0 THEN 'Paid'
                WHEN PaidAmount > 0 THEN 'PartiallyPaid'
                ELSE 'Unpaid'
            END
            WHERE PaymentStatus IS NULL OR PaymentStatus NOT IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue');
            IF OBJECT_ID(N'[dbo].[CK_SalesInvoice_PaymentStatus]', N'C') IS NOT NULL
                ALTER TABLE SalesInvoice DROP CONSTRAINT CK_SalesInvoice_PaymentStatus;
            ALTER TABLE SalesInvoice WITH CHECK ADD CONSTRAINT CK_SalesInvoice_PaymentStatus
                CHECK (PaymentStatus IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue'));
        END;

        IF OBJECT_ID(N'[dbo].[PurchaseInvoice]', N'U') IS NOT NULL
        BEGIN
            UPDATE PurchaseInvoice SET PaymentStatus = 'Unpaid' WHERE UPPER(PaymentStatus) = 'UNPAID';
            UPDATE PurchaseInvoice SET PaymentStatus = 'PartiallyPaid' WHERE UPPER(PaymentStatus) IN ('PARTIAL', 'PARTIALLYPAID');
            UPDATE PurchaseInvoice SET PaymentStatus = 'Paid' WHERE UPPER(PaymentStatus) = 'PAID';
            UPDATE PurchaseInvoice SET PaymentStatus = 'Overdue' WHERE UPPER(PaymentStatus) = 'OVERDUE';
            UPDATE PurchaseInvoice
            SET PaymentStatus = CASE
                WHEN PaidAmount >= GrandTotal AND GrandTotal > 0 THEN 'Paid'
                WHEN PaidAmount > 0 THEN 'PartiallyPaid'
                ELSE 'Unpaid'
            END
            WHERE PaymentStatus IS NULL OR PaymentStatus NOT IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue');
            IF OBJECT_ID(N'[dbo].[CK_PurchaseInvoice_PaymentStatus]', N'C') IS NOT NULL
                ALTER TABLE PurchaseInvoice DROP CONSTRAINT CK_PurchaseInvoice_PaymentStatus;
            ALTER TABLE PurchaseInvoice WITH CHECK ADD CONSTRAINT CK_PurchaseInvoice_PaymentStatus
                CHECK (PaymentStatus IN ('Unpaid', 'PartiallyPaid', 'Paid', 'Overdue'));
        END;
        
GO
-- SchemaVersion6Sql
-- danh sách cố định giới hạn dynamic SQL vào đúng các bảng nghiệp vụ cần chống ghi đè.

        DECLARE @MutableTables TABLE ([TableName] SYSNAME NOT NULL PRIMARY KEY);
        INSERT INTO @MutableTables ([TableName])
        VALUES
            (N'AppUser'), (N'AuditArchiveManifest'), (N'Brand'), (N'Category'),
            (N'Customer'), (N'Product'), (N'ProductSerial'), (N'ProductUnit'),
            (N'PurchaseInvoice'), (N'PurchaseInvoiceLine'), (N'SalesInvoice'),
            (N'SalesInvoiceLine'), (N'StockAdjustment'), (N'StockAdjustmentLine'),
            (N'StockBalance'), (N'StockCountLine'), (N'StockCountSession'),
            (N'StockIn'), (N'StockInLine'), (N'StockOut'), (N'StockOutLine'),
            (N'StockTransfer'), (N'StockTransferLine'), (N'Supplier'), (N'Unit'),
            (N'Warehouse'), (N'WarrantyClaim'), (N'WarrantyCoverage');

        DECLARE @TableName SYSNAME;
        WHILE EXISTS (SELECT 1 FROM @MutableTables)
        BEGIN
            SELECT TOP (1) @TableName = [TableName] FROM @MutableTables ORDER BY [TableName];
            IF OBJECT_ID(N'[dbo].' + QUOTENAME(@TableName), N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.' + @TableName, N'RowVersion') IS NULL
            BEGIN
                DECLARE @AddRowVersionSql NVARCHAR(MAX) =
                    N'ALTER TABLE [dbo].' + QUOTENAME(@TableName) +
                    N' ADD [RowVersion] ROWVERSION NOT NULL;';
                EXEC sys.sp_executesql @AddRowVersionSql;
            END;
            DELETE FROM @MutableTables WHERE [TableName] = @TableName;
        END;

        -- heartbeat phiên client giúp quá trình nâng cấp biết máy nào vẫn đang dùng database.
        IF OBJECT_ID(N'[dbo].[__WareProClientSession]', N'U') IS NULL
        BEGIN
            CREATE TABLE [dbo].[__WareProClientSession]
            (
                [SessionId] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [PK___WareProClientSession] PRIMARY KEY,
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
            SELECT 1 FROM sys.indexes
            WHERE [object_id] = OBJECT_ID(N'[dbo].[__WareProClientSession]')
              AND [name] = N'IX___WareProClientSession_LastSeenUtc'
        )
            CREATE INDEX [IX___WareProClientSession_LastSeenUtc]
                ON [dbo].[__WareProClientSession] ([LastSeenUtc]);


GO
-- SchemaVersion7Sql
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
    ALTER TABLE dbo.PurchaseInvoice WITH CHECK
        ADD CONSTRAINT CK_PurchaseInvoice_Status
            CHECK ([Status] IN (N'Active', N'Voided'));
    ALTER TABLE dbo.PurchaseInvoice CHECK CONSTRAINT CK_PurchaseInvoice_Status;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.PurchaseInvoice')
      AND name = N'IX_PurchaseInvoice_Status_InvoiceDate'
)
BEGIN
    CREATE INDEX IX_PurchaseInvoice_Status_InvoiceDate
        ON dbo.PurchaseInvoice([Status], InvoiceDate);
END;

IF COL_LENGTH(N'dbo.SalesInvoice', N'Status') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoice
        ADD [Status] NVARCHAR(20) NOT NULL
            CONSTRAINT DF_SalesInvoice_Status DEFAULT (N'Active');
END;

IF OBJECT_ID(N'dbo.CK_SalesInvoice_Status', N'C') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoice WITH CHECK
        ADD CONSTRAINT CK_SalesInvoice_Status
            CHECK ([Status] IN (N'Active', N'Voided'));
    ALTER TABLE dbo.SalesInvoice CHECK CONSTRAINT CK_SalesInvoice_Status;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.SalesInvoice')
      AND name = N'IX_SalesInvoice_Status_InvoiceDate'
)
BEGIN
    CREATE INDEX IX_SalesInvoice_Status_InvoiceDate
        ON dbo.SalesInvoice([Status], InvoiceDate);
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
    CREATE UNIQUE INDEX UX_WarrantyClaim_OpenProductSerialId
        ON dbo.WarrantyClaim(OpenProductSerialId)
        WHERE OpenProductSerialId IS NOT NULL;
END;

IF OBJECT_ID(N'dbo.__WareProSchemaVersion', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.__WareProSchemaVersion
    SET [Version] = 7,
        UpdatedAt = SYSUTCDATETIME()
    WHERE Id = 1 AND [Version] < 7;
END;

-- SchemaArchiveReplaySql

        IF OBJECT_ID(N'[dbo].[AuditArchiveManifest]', N'U') IS NOT NULL
        BEGIN
            IF COL_LENGTH(N'dbo.AuditArchiveManifest', N'OperationId') IS NULL
                ALTER TABLE [dbo].[AuditArchiveManifest]
                    ADD [OperationId] UNIQUEIDENTIFIER NULL;

            UPDATE [dbo].[AuditArchiveManifest]
            SET [OperationId] = NEWID()
            WHERE [OperationId] IS NULL;

            IF EXISTS
            (
                SELECT 1
                FROM sys.columns
                WHERE [object_id] = OBJECT_ID(N'[dbo].[AuditArchiveManifest]')
                  AND [name] = N'OperationId'
                  AND [is_nullable] = 1
            )
                ALTER TABLE [dbo].[AuditArchiveManifest]
                    ALTER COLUMN [OperationId] UNIQUEIDENTIFIER NOT NULL;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic
                    ON ic.[object_id] = i.[object_id]
                   AND ic.[index_id] = i.[index_id]
                   AND ic.[key_ordinal] = 1
                INNER JOIN sys.columns AS c
                    ON c.[object_id] = ic.[object_id]
                   AND c.[column_id] = ic.[column_id]
                WHERE i.[object_id] = OBJECT_ID(N'[dbo].[AuditArchiveManifest]')
                  AND i.[name] = N'UX_AuditArchiveManifest_OperationId'
                  AND i.[is_unique] = 1
                  AND c.[name] = N'OperationId'
                  AND 1 =
                  (
                      SELECT COUNT(*)
                      FROM sys.index_columns AS keys
                      WHERE keys.[object_id] = i.[object_id]
                        AND keys.[index_id] = i.[index_id]
                        AND keys.[key_ordinal] > 0
                  )
            )
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[AuditArchiveManifest]')
                      AND [name] = N'UX_AuditArchiveManifest_OperationId'
                )
                    DROP INDEX [UX_AuditArchiveManifest_OperationId]
                        ON [dbo].[AuditArchiveManifest];

                CREATE UNIQUE INDEX [UX_AuditArchiveManifest_OperationId]
                    ON [dbo].[AuditArchiveManifest] ([OperationId]);
            END;
        END;
        
GO
