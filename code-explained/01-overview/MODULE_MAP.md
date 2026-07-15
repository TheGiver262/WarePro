# Ban do module WarePro

| Khu vuc | Diem vao | Service/loi | Test lien quan |
|---|---|---|---|
| Startup/login | `App.xaml.cs`, `LoginView*` | `DatabaseInitializer`, `AuthenticationService` | initializer va seed policy |
| Shell/dieu huong | `MainWindow*`, `MainViewModel.cs` | `AuthorizationService` | MainViewModel va RBAC |
| Danh muc | cac `Category/Brand/Unit/Supplier/CustomerView*` | service cung ten | service, ViewModel, view |
| San pham/serial | `ProductView*`, `ProductSerialView*` | `ProductService`, `ProductSerialService` | product/serial/import |
| Nhap/xuat kho | `StockInView*`, `StockOutView*` | service cung ten, `InventoryPostingService` | inventory va ViewModel |
| Chuyen/dieu chinh | `StockTransferView*`, `StockAdjustmentView*` | service va inventory policies | inventory integration |
| Kiem ke | `StockCountView*` | `StockCountService` | stock count |
| Ton dau ky | `OpeningBalanceImportView*` | `OpeningBalanceImportService` | import va seed |
| Ton kho | `InventoryView*` | truy van `StockBalance` | inventory |
| Hoa don | `PurchaseInvoiceView*`, `SalesInvoiceView*` | `InvoiceService`, print | invoice va print |
| Quyen bao hanh | `WarrantyCoverageView*` | coverage queries | warranty coverage |
| Xu ly bao hanh | `WarrantyView*`, dialog | `WarrantyClaimService` | warranty claim |
| Bao cao/truy vet | `ReportView*` | `ReportTraceService` | report trace |
| In an | `DocumentPrintWindow*` | `DocumentPrintService` | document print |
| Nguoi dung/audit | `AppUserView*`, `AuditLogView*` | user, audit, authorization | RBAC/audit/user |
| Du lieu | `AppDbContext.cs`, `Models/`, `Migrations/` | EF Core SQL Server | database/seed |

Quy uoc tim: `XxxView.xaml` <-> `XxxViewModel.cs` <-> `XxxService.cs` <-> test chua `Xxx`.
