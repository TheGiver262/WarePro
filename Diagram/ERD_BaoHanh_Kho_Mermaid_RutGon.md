# Sơ đồ ERD quản lý hàng hóa và bảo hành (Mermaid Syntax - Rút gọn)

Sơ đồ ERD tổng thể của dự án (theo phiên bản PlantUML rút gọn thực tế trong phần mềm) được biểu diễn bằng cú pháp Mermaid dưới đây.

```mermaid
%%{init: { "theme": "default", "themeVariables": { "background": "#ffffff" } } }%%
erDiagram
    AppUser {
        int Id PK
        string Username UQ
        string FullName
        string RoleCode
        bool IsActive
    }

    Category {
        int Id PK
        string CategoryCode UQ
        string DisplayName
        bool IsActive
    }

    Brand {
        int Id PK
        string BrandCode UQ
        string DisplayName
        bool IsActive
    }

    Unit {
        int Id PK
        string UnitCode UQ
        string DisplayName
        bool IsActive
    }

    Supplier {
        int Id PK
        string SupplierCode UQ
        string DisplayName
        bool IsActive
    }

    Customer {
        int Id PK
        string CustomerCode UQ
        string DisplayName
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UQ
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    Product {
        int Id PK
        string ProductCode UQ
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        bool IsSerialTracked
        bool IsActive
    }

    ProductUnit {
        int Id PK
        int ProductId FK
        int UnitId FK
        decimal ConversionFactor
        bool IsBaseUnit
    }

    StockBalance {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        decimal OnHandQuantity
        decimal AvailableQuantity
    }

    StockIn {
        int Id PK
        string DocumentCode UQ
        int SupplierId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        datetime CreatedAt
    }

    StockInLine {
        int Id PK
        int StockInId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
    }

    StockOut {
        int Id PK
        string DocumentCode UQ
        int CustomerId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        datetime CreatedAt
    }

    StockOutLine {
        int Id PK
        int StockOutId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
    }

    StockCountSession {
        int Id PK
        string SessionCode UQ
        int WarehouseId FK
        string Status
        int CreatedBy FK
        datetime CountDate
    }

    StockCountLine {
        int Id PK
        int SessionId FK
        int ProductId FK
        decimal SystemQuantity
        decimal CountedQuantity
        decimal VarianceQuantity
    }

    ProductSerial {
        int Id PK
        int ProductId FK
        string SerialNumber UQ
        string CurrentStatus
        int CurrentWarehouseId FK
    }

    StockLedger {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        int ProductSerialId FK
        string SourceDocumentType
        int SourceDocumentId
        string MovementType
        decimal Quantity
    }

    PurchaseInvoice {
        int Id PK
        string InvoiceCode UQ
        int SupplierId FK
        int StockInId FK
        datetime InvoiceDate
        decimal GrandTotal
        string PaymentStatus
    }

    PurchaseInvoiceLine {
        int Id PK
        int PurchaseInvoiceId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal GrandTotal
    }

    SalesInvoice {
        int Id PK
        string InvoiceCode UQ
        int CustomerId FK
        int StockOutId FK
        datetime InvoiceDate
        decimal GrandTotal
        string PaymentStatus
    }

    SalesInvoiceLine {
        int Id PK
        int SalesInvoiceId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal GrandTotal
    }

    WarrantyCoverage {
        int Id PK
        int ProductSerialId FK
        int CustomerId FK
        int SalesInvoiceId FK
        datetime WarrantyStartDate
        datetime WarrantyEndDate
        string CoverageStatus
    }

    WarrantyClaim {
        int Id PK
        string ClaimCode UQ
        int WarrantyCoverageId FK
        int ProductSerialId FK
        int ReplacementSerialId FK
        int ReplacementStockOutId FK
        string ProblemDescription
        string Status
        int ProcessedBy FK
    }

    AuditLog {
        int Id PK
        string EntityName
        int EntityId
        string ActionCode
        int PerformedBy FK
        datetime PerformedAt
    }

    %% Relationships
    Category ||--o{ Product : "categorizes"
    Brand ||--o{ Product : "manufactures"
    Unit ||--o{ Product : "default_unit"
    Product ||--o{ ProductUnit : "has_units"
    Unit ||--o{ ProductUnit : "used_in"

    Warehouse ||--o{ StockBalance : "balances"
    Product ||--o{ StockBalance : "stocked_in"

    Supplier |o--o{ StockIn : "supplies"
    Warehouse ||--o{ StockIn : "stores"
    StockIn ||--o{ StockInLine : "contains"
    Product ||--o{ StockInLine : "item"
    Unit ||--o{ StockInLine : "uom"

    Warehouse ||--o{ StockOut : "delivers_from"
    Customer ||--o{ StockOut : "receives"
    StockOut ||--o{ StockOutLine : "contains"
    Product ||--o{ StockOutLine : "item"
    Unit ||--o{ StockOutLine : "uom"

    Warehouse ||--o{ StockCountSession : "audits"
    StockCountSession ||--o{ StockCountLine : "contains"
    Product ||--o{ StockCountLine : "item"

    Supplier ||--o{ PurchaseInvoice : "bills"
    StockIn |o--o| PurchaseInvoice : "links_to"
    PurchaseInvoice ||--o{ PurchaseInvoiceLine : "contains"
    Product ||--o{ PurchaseInvoiceLine : "item"
    Unit ||--o{ PurchaseInvoiceLine : "uom"
    StockInLine |o--o{ PurchaseInvoiceLine : "referenced_by"

    Customer ||--o{ SalesInvoice : "invoices"
    StockOut |o--o| SalesInvoice : "links_to"
    SalesInvoice ||--o{ SalesInvoiceLine : "contains"
    Product ||--o{ SalesInvoiceLine : "item"
    Unit ||--o{ SalesInvoiceLine : "uom"
    StockOutLine |o--o{ SalesInvoiceLine : "referenced_by"

    Warehouse |o--o{ ProductSerial : "located_at"
    Product ||--o{ ProductSerial : "has_serials"
    StockInLine ||--o{ ProductSerial : "registered_by"
    StockOutLine |o--o{ ProductSerial : "released_by"

    Warehouse ||--o{ StockLedger : "logs"
    Product ||--o{ StockLedger : "tracks"
    ProductSerial |o--o{ StockLedger : "serial_tracks"

    ProductSerial ||--o{ WarrantyCoverage : "covers"
    Customer ||--o{ WarrantyCoverage : "owned_by"
    SalesInvoice |o--o{ WarrantyCoverage : "originates_from"

    WarrantyCoverage ||--o{ WarrantyClaim : "asserts"
    ProductSerial ||--o{ WarrantyClaim : "claims"
    ProductSerial |o--o{ WarrantyClaim : "replaced_by"
    StockOut |o--o| WarrantyClaim : "dispatched_by"

    AppUser |o--o{ AppUser : "creates"
    AppUser ||--o{ StockIn : "creates"
    AppUser |o--o{ StockIn : "approves"
    AppUser |o--o{ StockIn : "posts"
    AppUser ||--o{ StockOut : "creates"
    AppUser |o--o{ StockOut : "approves"
    AppUser |o--o{ StockOut : "posts"
    AppUser ||--o{ StockCountSession : "creates"
    AppUser |o--o{ StockCountSession : "approves"
    AppUser |o--o{ StockCountSession : "posts"
    AppUser ||--o{ StockLedger : "posts"
    AppUser |o--o{ WarrantyClaim : "approves"
    AppUser ||--o{ WarrantyClaim : "processes"
    AppUser ||--o{ AuditLog : "logs"
```
