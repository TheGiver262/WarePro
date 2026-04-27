# ERD module

Ghi chú:
- Bộ sơ đồ này được tách từ ERD chi tiết để dễ đọc theo từng phân hệ.
- Một số bảng được lặp lại giữa các module để giữ ngữ cảnh đầy đủ trong từng sơ đồ.
- Mỗi bảng trong module vẫn giữ đủ field theo ERD chi tiết hiện hành.
- `Warehouse` được thiết kế future-ready, nhưng phase hiện tại chỉ có một kho mặc định và UI không cho người dùng chọn kho.

---

## Module 1. Core & Catalog

```mermaid
erDiagram
    Category {
        int Id PK
        string CategoryCode
        string DisplayName
        bool IsActive
    }

    Brand {
        int Id PK
        string BrandCode
        string DisplayName
        string OriginCountry
        bool IsActive
    }

    Unit {
        int Id PK
        string UnitCode
        string DisplayName
        bool IsActive
    }

    Supplier {
        int Id PK
        string SupplierCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Customer {
        int Id PK
        string CustomerCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UK
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    Product {
        int Id PK
        string ProductCode UK
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        decimal DefaultPrice
        string OriginCountry
        int WarrantyPeriodMonths
        bool IsSerialTracked
        bool IsActive
    }

    ProductUnit {
        int Id PK
        int ProductId FK
        int UnitId FK
        decimal ConversionFactor
        bool IsBaseUnit
        bool IsPurchaseUnit
        bool IsSalesUnit
        string UQ_ProductUnit "UNIQUE (ProductId, UnitId)"
    }

    Category ||--o{ Product : categoryId
    Brand ||--o{ Product : brandId
    Unit ||--o{ Product : defaultUnitId
    Product ||--o{ ProductUnit : productId
    Unit ||--o{ ProductUnit : unitId
```

Ghi chú module:
- `Product.ProductCode` phải unique.
- `Warehouse` phase hiện tại chỉ có một dòng mặc định, nhưng vẫn là thực thể thật để tránh phải phá schema khi mở rộng nhiều kho.
- `ProductUnit` cần unique `(ProductId, UnitId)` và mỗi sản phẩm chỉ có một dòng `IsBaseUnit = true`.

---

## Module 2. Inventory Flow

```mermaid
erDiagram
    AppUser {
        int Id PK
        string Username
        string PasswordHash
        string FullName
        string RoleCode
        bool MustChangePassword
        int FailedLoginCount
        int CreatedBy FK "nullable"
        datetime CreatedAt
        datetime LockoutUntil "nullable"
        datetime LastFailedLoginAt "nullable"
        datetime LastPasswordChangedAt "nullable"
        datetime LastLoginAt "nullable"
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UK
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    Supplier {
        int Id PK
        string SupplierCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Customer {
        int Id PK
        string CustomerCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Product {
        int Id PK
        string ProductCode UK
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        decimal DefaultPrice
        string OriginCountry
        int WarrantyPeriodMonths
        bool IsSerialTracked
        bool IsActive
    }

    Unit {
        int Id PK
        string UnitCode
        string DisplayName
        bool IsActive
    }

    StockBalance {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        decimal OnHandQuantity
        decimal AvailableQuantity
        decimal ReservedQuantity
        string UQ_StockBalance "UNIQUE (WarehouseId, ProductId)"
    }

    StockIn {
        int Id PK
        string DocumentCode UK
        int SupplierId FK "nullable"
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockInLine {
        int Id PK
        int StockInId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
        decimal UnitPrice
    }

    StockOut {
        int Id PK
        string DocumentCode UK
        int CustomerId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockOutLine {
        int Id PK
        int StockOutId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
        decimal UnitPrice
    }

    StockCountSession {
        int Id PK
        string SessionCode UK
        int WarehouseId FK
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CountDate
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockCountLine {
        int Id PK
        int SessionId FK
        int ProductId FK
        decimal SystemQuantity
        decimal CountedQuantity
        decimal VarianceQuantity
    }

    StockAdjustment {
        int Id PK
        string DocumentCode UK
        int WarehouseId FK
        string AdjustmentType
        string Status
        string ReferenceDocumentType
        int ReferenceDocumentId
        string ReasonCode
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockAdjustmentLine {
        int Id PK
        int AdjustmentId FK
        int ProductId FK
        int ProductSerialId FK "nullable"
        decimal QuantityDelta
        decimal BaseQuantityDelta
        string Direction
    }

    ProductSerial {
        int Id PK
        int ProductId FK
        string SerialNumber UK
        string CurrentStatus
        int CurrentWarehouseId FK "nullable"
        int LastStockInLineId FK
        int LastStockOutLineId FK "nullable"
    }

    StockLedger {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        int ProductSerialId FK "nullable"
        string SourceDocumentType
        int SourceDocumentId
        string MovementType
        decimal Quantity
        int PostedBy FK
        datetime PostedAt
    }

    Warehouse ||--o{ StockBalance : warehouseId
    Product ||--o{ StockBalance : productId

    Supplier |o--o{ StockIn : supplierId
    Warehouse ||--o{ StockIn : warehouseId
    StockIn ||--o{ StockInLine : stockInId
    Product ||--o{ StockInLine : productId
    Unit ||--o{ StockInLine : unitId

    Warehouse ||--o{ StockOut : warehouseId
    Customer ||--o{ StockOut : customerId
    StockOut ||--o{ StockOutLine : stockOutId
    Product ||--o{ StockOutLine : productId
    Unit ||--o{ StockOutLine : unitId

    Warehouse ||--o{ StockCountSession : warehouseId
    StockCountSession ||--o{ StockCountLine : sessionId
    Product ||--o{ StockCountLine : productId

    Warehouse ||--o{ StockAdjustment : warehouseId
    StockAdjustment ||--o{ StockAdjustmentLine : adjustmentId
    Product ||--o{ StockAdjustmentLine : productId
    ProductSerial |o--o{ StockAdjustmentLine : productSerialId

    Warehouse |o--o{ ProductSerial : currentWarehouseId
    Product ||--o{ ProductSerial : productId
    StockInLine ||--o{ ProductSerial : lastStockInLineId
    StockOutLine |o--o{ ProductSerial : lastStockOutLineId

    Warehouse ||--o{ StockLedger : warehouseId
    Product ||--o{ StockLedger : productId
    ProductSerial |o--o{ StockLedger : productSerialId
    AppUser ||--o{ StockLedger : postedBy

    AppUser |o--o{ AppUser : createdBy
    AppUser ||--o{ StockIn : createdBy
    AppUser |o--o{ StockIn : approvedBy
    AppUser |o--o{ StockIn : postedBy
    AppUser ||--o{ StockOut : createdBy
    AppUser |o--o{ StockOut : approvedBy
    AppUser |o--o{ StockOut : postedBy
    AppUser ||--o{ StockCountSession : createdBy
    AppUser |o--o{ StockCountSession : approvedBy
    AppUser |o--o{ StockCountSession : postedBy
    AppUser ||--o{ StockAdjustment : createdBy
    AppUser |o--o{ StockAdjustment : approvedBy
    AppUser |o--o{ StockAdjustment : postedBy
```

Ghi chú module:
- `StockBalance` phải unique theo `(ProductId, WarehouseId)`.
- Giai đoạn hiện tại luôn dùng kho mặc định khi nhập/xuất/kiểm kê/điều chỉnh.
- `StockIn.PurposeCode` tối thiểu gồm `Purchase` và `OpeningBalance`.
- `StockOut.PurposeCode` tối thiểu gồm `Sale` và `WarrantyReplacement`.
- Các transaction ghi sổ phải khóa `StockBalance` theo thứ tự `ProductId` tăng dần để giảm rủi ro deadlock.

---

## Module 3. Invoicing

```mermaid
erDiagram
    Supplier {
        int Id PK
        string SupplierCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Customer {
        int Id PK
        string CustomerCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UK
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    Product {
        int Id PK
        string ProductCode UK
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        decimal DefaultPrice
        string OriginCountry
        int WarrantyPeriodMonths
        bool IsSerialTracked
        bool IsActive
    }

    Unit {
        int Id PK
        string UnitCode
        string DisplayName
        bool IsActive
    }

    StockIn {
        int Id PK
        string DocumentCode UK
        int SupplierId FK "nullable"
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockInLine {
        int Id PK
        int StockInId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
        decimal UnitPrice
    }

    StockOut {
        int Id PK
        string DocumentCode UK
        int CustomerId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockOutLine {
        int Id PK
        int StockOutId FK
        int ProductId FK
        int UnitId FK
        decimal Quantity
        decimal BaseQuantity
        decimal UnitPrice
    }

    PurchaseInvoice {
        int Id PK
        string InvoiceCode UK
        int SupplierId FK
        int StockInId FK "nullable"
        datetime InvoiceDate
        decimal SubTotal
        decimal TaxAmount
        decimal GrandTotal
        decimal PaidAmount
        string PaymentStatus
        datetime DueDate
    }

    PurchaseInvoiceLine {
        int Id PK
        int PurchaseInvoiceId FK
        int ProductId FK
        int UnitId FK
        int StockInLineId FK "nullable"
        decimal Quantity
        decimal UnitPrice
        decimal SubTotal
        decimal TaxRate
        decimal TaxAmount
        decimal GrandTotal
    }

    SalesInvoice {
        int Id PK
        string InvoiceCode UK
        int CustomerId FK
        int StockOutId FK "nullable"
        datetime InvoiceDate
        decimal SubTotal
        decimal TaxAmount
        decimal GrandTotal
        decimal PaidAmount
        string PaymentStatus
        datetime DueDate
    }

    SalesInvoiceLine {
        int Id PK
        int SalesInvoiceId FK
        int ProductId FK
        int UnitId FK
        int StockOutLineId FK "nullable"
        decimal Quantity
        decimal UnitPrice
        decimal SubTotal
        decimal TaxRate
        decimal TaxAmount
        decimal GrandTotal
    }

    Supplier |o--o{ StockIn : supplierId
    Warehouse ||--o{ StockIn : warehouseId
    StockIn ||--o{ StockInLine : stockInId
    Product ||--o{ StockInLine : productId
    Unit ||--o{ StockInLine : unitId

    Customer ||--o{ StockOut : customerId
    Warehouse ||--o{ StockOut : warehouseId
    StockOut ||--o{ StockOutLine : stockOutId
    Product ||--o{ StockOutLine : productId
    Unit ||--o{ StockOutLine : unitId

    Supplier ||--o{ PurchaseInvoice : supplierId
    StockIn |o--o| PurchaseInvoice : stockInId
    PurchaseInvoice ||--o{ PurchaseInvoiceLine : purchaseInvoiceId
    Product ||--o{ PurchaseInvoiceLine : productId
    Unit ||--o{ PurchaseInvoiceLine : unitId
    StockInLine |o--o{ PurchaseInvoiceLine : stockInLineId

    Customer ||--o{ SalesInvoice : customerId
    StockOut |o--o| SalesInvoice : stockOutId
    SalesInvoice ||--o{ SalesInvoiceLine : salesInvoiceId
    Product ||--o{ SalesInvoiceLine : productId
    Unit ||--o{ SalesInvoiceLine : unitId
    StockOutLine |o--o{ SalesInvoiceLine : stockOutLineId
```

Ghi chú module:
- Thuế phase này chỉ lưu `SubTotal`, `TaxRate`, `TaxAmount`, `GrandTotal` để tính tiền và in hóa đơn.
- Không hỗ trợ nghiệp vụ thuế kế toán phức tạp, kê khai thuế hoặc nhiều sắc thuế.
- `PurchaseInvoice.StockInId` và `SalesInvoice.StockOutId` là nullable.

---

## Module 4. Warranty

```mermaid
erDiagram
    AppUser {
        int Id PK
        string Username
        string PasswordHash
        string FullName
        string RoleCode
        bool MustChangePassword
        int FailedLoginCount
        int CreatedBy FK "nullable"
        datetime CreatedAt
        datetime LockoutUntil "nullable"
        datetime LastFailedLoginAt "nullable"
        datetime LastPasswordChangedAt "nullable"
        datetime LastLoginAt "nullable"
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UK
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    Customer {
        int Id PK
        string CustomerCode
        string DisplayName
        string Phone
        string Email
        bool IsActive
    }

    Product {
        int Id PK
        string ProductCode UK
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        decimal DefaultPrice
        string OriginCountry
        int WarrantyPeriodMonths
        bool IsSerialTracked
        bool IsActive
    }

    ProductSerial {
        int Id PK
        int ProductId FK
        string SerialNumber UK
        string CurrentStatus
        int CurrentWarehouseId FK "nullable"
        int LastStockInLineId FK
        int LastStockOutLineId FK "nullable"
    }

    StockOut {
        int Id PK
        string DocumentCode UK
        int CustomerId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    SalesInvoice {
        int Id PK
        string InvoiceCode UK
        int CustomerId FK
        int StockOutId FK "nullable"
        datetime InvoiceDate
        decimal SubTotal
        decimal TaxAmount
        decimal GrandTotal
        decimal PaidAmount
        string PaymentStatus
        datetime DueDate
    }

    WarrantyCoverage {
        int Id PK
        int ProductSerialId FK
        int CustomerId FK
        int SalesInvoiceId FK "nullable"
        datetime WarrantyStartDate
        datetime WarrantyEndDate
        string CoverageStatus
        string UX_ActiveCoverage "FILTERED UNIQUE Active/ProductSerialId"
    }

    WarrantyClaim {
        int Id PK
        string ClaimCode UK
        int WarrantyCoverageId FK
        int ProductSerialId FK
        int ReplacementSerialId FK "nullable"
        int ReplacementStockOutId FK "nullable"
        datetime ReceivedDate
        string ProblemDescription
        string TechnicalConclusion
        string ManufacturerResult
        string RejectionReason
        string ProcessingNote
        string ResolutionType
        string Status
        int ApprovedBy FK "nullable"
        int ProcessedBy FK
        datetime ClosedDate "nullable"
        string UX_OpenClaim "FILTERED UNIQUE Open/ProductSerialId"
    }

    StockLedger {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        int ProductSerialId FK "nullable"
        string SourceDocumentType
        int SourceDocumentId
        string MovementType
        decimal Quantity
        int PostedBy FK
        datetime PostedAt
    }

    Product ||--o{ ProductSerial : productId
    Warehouse |o--o{ ProductSerial : currentWarehouseId
    Warehouse ||--o{ StockOut : warehouseId
    Customer ||--o{ StockOut : customerId
    Customer ||--o{ SalesInvoice : customerId
    StockOut |o--o| SalesInvoice : stockOutId

    ProductSerial ||--o{ WarrantyCoverage : productSerialId
    Customer ||--o{ WarrantyCoverage : customerId
    SalesInvoice |o--o{ WarrantyCoverage : salesInvoiceId

    WarrantyCoverage ||--o{ WarrantyClaim : warrantyCoverageId
    ProductSerial ||--o{ WarrantyClaim : productSerialId
    ProductSerial |o--o{ WarrantyClaim : replacementSerialId
    StockOut |o--o| WarrantyClaim : replacementStockOutId

    Warehouse ||--o{ StockLedger : warehouseId
    Product ||--o{ StockLedger : productId
    ProductSerial |o--o{ StockLedger : productSerialId

    AppUser |o--o{ AppUser : createdBy
    AppUser |o--o{ WarrantyClaim : approvedBy
    AppUser ||--o{ WarrantyClaim : processedBy
    AppUser ||--o{ StockOut : createdBy
    AppUser |o--o{ StockOut : approvedBy
    AppUser |o--o{ StockOut : postedBy
    AppUser ||--o{ StockLedger : postedBy
```

Ghi chú module:
- `WarrantyCoverage.SalesInvoiceId` nullable vì coverage của serial thay thế không bắt buộc sinh từ hóa đơn bán mới.
- `WarrantyClaim.ReplacementSerialId` và `ReplacementStockOutId` chỉ có ở nhánh đổi mới.
- Đổi mới bảo hành sinh `StockOut` với `PurposeCode = WarrantyReplacement`.

---

## Module 5. User & Audit

```mermaid
erDiagram
    AppUser {
        int Id PK
        string Username
        string PasswordHash
        string FullName
        string RoleCode
        bool MustChangePassword
        int FailedLoginCount
        int CreatedBy FK "nullable"
        datetime CreatedAt
        datetime LockoutUntil "nullable"
        datetime LastFailedLoginAt "nullable"
        datetime LastPasswordChangedAt "nullable"
        datetime LastLoginAt "nullable"
        bool IsActive
    }

    Warehouse {
        int Id PK
        string WarehouseCode UK
        string DisplayName
        bool IsDefault
        bool IsActive
    }

    StockIn {
        int Id PK
        string DocumentCode UK
        int SupplierId FK "nullable"
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockOut {
        int Id PK
        string DocumentCode UK
        int CustomerId FK
        int WarehouseId FK
        string PurposeCode
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CreatedAt
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockCountSession {
        int Id PK
        string SessionCode UK
        int WarehouseId FK
        string Status
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime CountDate
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    StockAdjustment {
        int Id PK
        string DocumentCode UK
        int WarehouseId FK
        string AdjustmentType
        string Status
        string ReferenceDocumentType
        int ReferenceDocumentId
        string ReasonCode
        int CreatedBy FK
        int ApprovedBy FK "nullable"
        int PostedBy FK "nullable"
        datetime ApprovedAt "nullable"
        datetime PostedAt "nullable"
    }

    Product {
        int Id PK
        string ProductCode UK
        string DisplayName
        int CategoryId FK
        int BrandId FK
        int DefaultUnitId FK
        decimal DefaultPrice
        string OriginCountry
        int WarrantyPeriodMonths
        bool IsSerialTracked
        bool IsActive
    }

    ProductSerial {
        int Id PK
        int ProductId FK
        string SerialNumber UK
        string CurrentStatus
        int CurrentWarehouseId FK "nullable"
        int LastStockInLineId FK
        int LastStockOutLineId FK "nullable"
    }

    StockLedger {
        int Id PK
        int WarehouseId FK
        int ProductId FK
        int ProductSerialId FK "nullable"
        string SourceDocumentType
        int SourceDocumentId
        string MovementType
        decimal Quantity
        int PostedBy FK
        datetime PostedAt
    }

    WarrantyClaim {
        int Id PK
        string ClaimCode UK
        int WarrantyCoverageId FK
        int ProductSerialId FK
        int ReplacementSerialId FK "nullable"
        int ReplacementStockOutId FK "nullable"
        datetime ReceivedDate
        string ProblemDescription
        string TechnicalConclusion
        string ManufacturerResult
        string RejectionReason
        string ProcessingNote
        string ResolutionType
        string Status
        int ApprovedBy FK "nullable"
        int ProcessedBy FK
        datetime ClosedDate "nullable"
    }

    AuditLog {
        int Id PK
        string EntityName
        int EntityId
        string ActionCode
        string BeforeJson
        string AfterJson
        int PerformedBy FK
        datetime PerformedAt
    }

    AppUser |o--o{ AppUser : createdBy
    AppUser ||--o{ StockIn : createdBy
    AppUser |o--o{ StockIn : approvedBy
    AppUser |o--o{ StockIn : postedBy
    AppUser ||--o{ StockOut : createdBy
    AppUser |o--o{ StockOut : approvedBy
    AppUser |o--o{ StockOut : postedBy
    AppUser ||--o{ StockCountSession : createdBy
    AppUser |o--o{ StockCountSession : approvedBy
    AppUser |o--o{ StockCountSession : postedBy
    AppUser ||--o{ StockAdjustment : createdBy
    AppUser |o--o{ StockAdjustment : approvedBy
    AppUser |o--o{ StockAdjustment : postedBy
    AppUser ||--o{ StockLedger : postedBy
    AppUser |o--o{ WarrantyClaim : approvedBy
    AppUser ||--o{ WarrantyClaim : processedBy
    AppUser ||--o{ AuditLog : performedBy

    Warehouse ||--o{ StockIn : warehouseId
    Warehouse ||--o{ StockOut : warehouseId
    Warehouse ||--o{ StockCountSession : warehouseId
    Warehouse ||--o{ StockAdjustment : warehouseId
    Warehouse ||--o{ StockLedger : warehouseId
    Warehouse |o--o{ ProductSerial : currentWarehouseId
    Product ||--o{ StockLedger : productId
    ProductSerial |o--o{ StockLedger : productSerialId
    ProductSerial ||--o{ WarrantyClaim : productSerialId
```

Ghi chú module:
- `ApprovedBy`, `PostedBy`, `ApprovedAt`, `PostedAt`, `ClosedDate` là nullable cho đến khi workflow đi tới transition tương ứng.
- `AuditLog` dùng để truy vết thay đổi nghiệp vụ và thay đổi danh mục/cấu hình nhạy cảm.
