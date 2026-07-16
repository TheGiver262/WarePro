# Báo cáo rà soát độ đầy đủ của bộ tài liệu giải thích code

File này ghi lại kết quả đối chiếu giữa tài liệu trong `code-explained/` và code hiện tại của dự án. Mục tiêu là giúp người bảo vệ đồ án biết phần nào đã có tài liệu tốt, phần nào vừa được bổ sung, và phần nào là rủi ro cần nói rõ nếu hội đồng hỏi.

## 1. Kết luận tổng quan

Bộ tài liệu cũ đã bao phủ tốt kiến trúc nền, đăng nhập, MVVM, database, nhập kho, xuất kho, lõi tồn kho và bảo hành. Tuy nhiên codebase hiện có thêm nhiều phân hệ nghiệp vụ đã hoàn chỉnh hơn tài liệu ban đầu, đặc biệt là:

- Kiểm kê kho, xử lý chênh lệch kiểm kê.
- Điều chỉnh kho trực tiếp.
- Chuyển kho nội bộ.
- Hóa đơn mua, hóa đơn bán, công nợ và tự sinh quyền bảo hành.
- Dashboard, báo cáo doanh thu, báo cáo xuất nhập tồn, thẻ kho và truy vết serial.
- Audit log, audit timeline, lưu trữ log.
- Chiến lược kiểm thử và cách giải thích test fail do SQL Server thật.
- Cài đặt hai mode, runtime config, SQL credential, startup an toàn, updater và release gate.

Các file bổ sung mới tập trung vào những vùng này:

- `06_kiem_ke_chuyen_kho_dieu_chinh.md`
- `07_hoa_don_dashboard_bao_cao.md`
- `08_audit_import_testing.md`
- `09_so_tay_bao_ve_hoi_dong.md`

## 2. Ma trận bao phủ tài liệu

| Nhóm code | File code chính | Tài liệu hiện có | Mức bao phủ | Ghi chú |
|---|---|---|---|---|
| Khởi động/cài đặt/cập nhật | `App.xaml.cs`, `Configuration/`, `Startup/`, `Updates/`, `WarePro.SetupHelper/` | `01_khoi_dong_va_dieu_huong.md`, `13_startup_dbcontext_mapping_de_hieu.md`, `14_cai_dat_cap_nhat_phat_hanh_de_hieu.md` | Tốt | Có credential, compatibility, lock, backup, updater và release gate. |
| Database/Models | `AppDbContext.cs`, `Models/*` | `02_models_va_database.md`, `database_and_domain_models.md` | Tốt | Cần nhớ DB thật dùng SQL Server, test đa số dùng SQLite. |
| Authentication | `AuthenticationService.cs`, `LoginViewModel.cs` | `authentication_service.md`, `01_khoi_dong_va_dieu_huong.md` | Tốt | Có BCrypt, lockout, audit login. |
| Authorization/RBAC | `AuthorizationService.cs`, `MainViewModel.cs` | `03_services_nghiep_vu.md`, `presentation_and_viewmodels.md` | Khá | Đủ để trả lời quyền admin/log. |
| Nhập kho | `StockInService.cs`, `StockInViewModel.cs` | `03_services_nghiep_vu.md`, `04_viewmodels_logic_giaodien.md`, `execution_and_data_flows.md` | Rất tốt | Đây là luồng được mô tả kỹ nhất. |
| Xuất kho | `StockOutService.cs`, `StockOutViewModel.cs` | `execution_and_data_flows.md`, `business_services_detailed.md` | Khá | Đã đủ ở mức luồng nghiệp vụ, không chi tiết bằng nhập kho. |
| Lõi tồn kho | `Inventory/*` | `05_views_va_inventory_layer.md`, `inventory_core_engine.md` | Rất tốt | Có Unit of Work, ledger, balance, serial invariant. |
| Chuyển kho | `StockTransferService.cs`, `StockTransferViewModel.cs` | `business_services_detailed.md`, file bổ sung 06 | Bổ sung mới | Menu hiện đang comment trong `MainWindow.xaml`, nhưng code service/viewmodel tồn tại. |
| Kiểm kê | `StockCountService.cs`, `StockCountViewModel.cs` | `business_services_detailed.md`, file bổ sung 06 | Bổ sung mới | Điểm cần nhấn mạnh: xử lý chênh lệch tạo phiếu nháp nhập/xuất điều chỉnh. |
| Điều chỉnh kho | `StockAdjustmentService.cs`, `StockAdjustmentViewModel.cs`, `InventoryAdjustmentService.cs` | `business_services_detailed.md`, file bổ sung 06 | Bổ sung mới | Menu hiện đang comment trong `MainWindow.xaml`, nhưng code service/viewmodel tồn tại. |
| Hóa đơn | `InvoiceService.cs`, `PurchaseInvoiceViewModel.cs`, `SalesInvoiceViewModel.cs` | `business_services_detailed.md`, `presentation_and_viewmodels.md`, file bổ sung 07 | Bổ sung mới | Tài liệu mới giải thích tính tiền, công nợ và sinh bảo hành sau hóa đơn bán. |
| Dashboard/Báo cáo | `DashboardService.cs`, `DashboardViewModel.cs`, `ReportViewModel.cs` | `presentation_and_viewmodels.md`, file bổ sung 07 | Bổ sung mới | Cần giải thích rõ dashboard là KPI nhanh, report là phân tích sâu. |
| Import dữ liệu | `Services/DataImport/*`, `OpeningBalanceImport*` | `opening_balance_import.md`, `business_services_detailed.md`, file bổ sung 08 | Bổ sung mới | Tài liệu mới gom thêm phân loại file, mapping cột, import động và test. |
| Audit | `ReportTraceService / AuditLogService.cs`, `AuditLogViewModel.cs`, `ReportViewModel.cs` | `execution_and_data_flows.md`, file bổ sung 08 | Bổ sung mới | Truy vet san pham/serial da gom vao Bao cao; view truy van rieng da duoc go bo. |
| Testing | `QuanLyHangHoa.Tests/*` | `warranty_tests.md`, file bổ sung 08 và chương 14 | Tốt | 591/591 test `Category!=RealDatabase` đạt tại `895a70a`; RealDatabase và Gate C vẫn phải chạy trên môi trường disposable. |

## 3. Các điểm hội đồng dễ hỏi

### 3.1 Vì sao có cả `StockBalance` và `StockLedger`?

`StockLedger` là lịch sử phát sinh, giống sổ cái. Nó giúp truy vết vì sao tồn kho thay đổi. `StockBalance` là số dư hiện tại, giúp màn hình tồn kho truy vấn nhanh. Khi ghi sổ, hệ thống cập nhật đồng thời cả hai trong transaction để vừa nhanh vừa truy vết được.

### 3.2 Vì sao phiếu kiểm kê không tự cập nhật tồn kho ngay?

Kiểm kê là nghiệp vụ nhạy cảm. Code chọn hướng kiểm soát: sau khi chốt kiểm kê, `StockCountService.ProcessResults()` tạo các phiếu nhập/xuất điều chỉnh dạng nháp. Người dùng có thể kiểm tra lại trước khi ghi sổ thật. Thiết kế này giảm nguy cơ sai lệch tồn kho do nhập nhầm số kiểm kê.

### 3.3 Vì sao hóa đơn bán tạo bảo hành?

Quyền bảo hành bắt đầu khi sản phẩm serial-tracked được bán cho khách. `InvoiceService.SaveSalesInvoice()` sau khi lưu hóa đơn sẽ tìm phiếu xuất kho liên kết, lấy serial đã bán, rồi tạo `WarrantyCoverage` cho từng serial nếu chưa có. Nhờ vậy bảo hành bám theo serial thật chứ không chỉ theo mã sản phẩm.

### 3.4 Vì sao dùng `Func<AppDbContext>` thay vì giữ một `DbContext` toàn app?

Ứng dụng desktop chạy lâu. Nếu giữ một `DbContext` duy nhất, dữ liệu dễ bị cache cũ, tăng bộ nhớ và khó xử lý đa luồng. `Func<AppDbContext>` giúp mỗi service tạo context ngắn hạn bằng `using var db = _contextFactory();`, dùng xong giải phóng.

### 3.5 Vì sao 591/591 test đạt nhưng vẫn chưa kết luận release ready?

Automated gate tại `895a70a` đạt 591/591 test `Category!=RealDatabase`, build sạch và Inno compile. Kết quả này chứng minh logic, persistence độc lập và contract installer/startup/updater; nó không thay thế ký thật, SQL Server disposable, VM sạch, multi-client, backup/restore và rollback thuộc Gate C.

## 4. Rủi ro tài liệu cần nói thẳng

- Một số văn bản cũ bị lỗi encoding khi xem qua terminal, nhưng file gốc vẫn là tài liệu tiếng Việt. Khi viết đồ án nên chuẩn hóa encoding UTF-8.
- Một số màn đã có code nhưng bị ẩn trên menu, ví dụ chuyển kho và điều chỉnh kho trong `MainWindow.xaml`.
- Có resource tên `PrimaryPurpleBrush` và màu indigo/purple trong theme, trong khi guideline dự án có quy tắc không dùng purple/violet.
- Startup dùng compatibility gate, application lock, verified backup và schema update idempotent; Gate C trên VM/SQL disposable vẫn phải đạt trước stable release.
- Connection string được tạo từ runtime settings; SQL password nằm trong Windows Credential Manager theo từng user và không được ghi vào JSON/log.

## 5. Cách dùng bộ tài liệu khi ôn bảo vệ

Đọc theo thứ tự:

1. `INDEX.md` để nắm bản đồ tổng thể.
2. `architecture_overview.md` để trả lời kiến trúc.
3. `01_khoi_dong_va_dieu_huong.md` để trả lời luồng mở app và điều hướng.
4. `02_models_va_database.md` và `database_and_domain_models.md` để trả lời dữ liệu.
5. `05_views_va_inventory_layer.md` và `inventory_core_engine.md` để trả lời tồn kho.
6. `06_kiem_ke_chuyen_kho_dieu_chinh.md` để trả lời các luồng kho nâng cao.
7. `07_hoa_don_dashboard_bao_cao.md` để trả lời hóa đơn, dashboard, report.
8. `08_audit_import_testing.md` để trả lời audit, import, test.
9. `09_so_tay_bao_ve_hoi_dong.md` để luyện phản xạ hỏi đáp.
