# WarePro Knowledge Hub

Đây là cổng đọc duy nhất cho tài liệu giải thích và học WarePro. Nội dung được chia theo mục đích; code, kế hoạch vận hành và artifact sinh ra không trộn lẫn với nhau.

## Bắt đầu nhanh

- Muốn hiểu toàn bộ dự án: đọc [WarePro — Toàn cảnh dự án](./01-overview/WAREPRO_PROJECT_DEEP_DIVE.md).
- Muốn học từ căn bản: mở [Lộ trình học WarePro](./02-learning/00_INDEX.md).
- Muốn tra cứu một luồng nghiệp vụ: xem nhóm [Nghiệp vụ và luồng dữ liệu](#04-nghiệp-vụ-và-luồng-dữ-liệu).
- Muốn áp dụng kinh nghiệm sang dự án khác: đọc [Reusable Engineering Playbook](./06-lessons/WAREPRO_REUSABLE_ENGINEERING_PLAYBOOK.md).

## Lộ trình đọc đề xuất

### Hiểu dự án để bảo trì

1. [Toàn cảnh WarePro](./01-overview/WAREPRO_PROJECT_DEEP_DIVE.md)
2. [Kiến trúc hiện tại](./01-overview/ARCHITECTURE.md)
3. [Mô hình và database](./03-code/02_models_va_database.md)
4. [Lõi inventory](./04-business/inventory_core_engine.md)
5. [Services nghiệp vụ](./03-code/business_services_detailed.md)
6. [ViewModel và giao diện](./03-code/04_viewmodels_logic_giaodien.md)
7. [Audit, import và kiểm thử](./04-business/08_audit_import_testing.md)

### Học để tự xây lại dự án

1. [C# cần biết](./02-learning/01_ngon_ngu_csharp_can_biet.md)
2. [WPF, XAML và MVVM](./02-learning/02_wpf_xaml_mvvm_can_biet.md)
3. [EF Core và SQL Server](./02-learning/03_ef_core_sql_server_can_biet.md)
4. [Kiến trúc WarePro](./02-learning/04_kien_truc_du_an_quan_ly_hang_hoa.md)
5. [Nghiệp vụ và thuật toán](./02-learning/05_nghiep_vu_chinh_va_thuat_toan.md)
6. [Tự code lại dự án](./02-learning/06_tu_code_lai_du_an_tu_dau.md)
7. [Workbook thực hành](./02-learning/12_workbook_tu_code_app_mini.md)

## 01. Tổng quan

| Tài liệu | Dùng khi |
|---|---|
| [WAREPRO_PROJECT_DEEP_DIVE.md](./01-overview/WAREPRO_PROJECT_DEEP_DIVE.md) | Cần hiểu dự án từ nghiệp vụ đến mã nguồn |
| [Thiết kế phần mềm.md](./01-overview/Thiết%20kế%20phần%20mềm.md) | Cần phạm vi và quyết định thiết kế nền tảng |
| [ARCHITECTURE.md](./01-overview/ARCHITECTURE.md) | Cần bản đồ kiến trúc code đang chạy |
| [architecture_overview.md](./01-overview/architecture_overview.md) | Cần giải thích kiến trúc mở rộng |
| [MODULE_MAP.md](./01-overview/MODULE_MAP.md) | Cần tìm điểm vào, service và test của module |
| [FILE_STRUCTURE.md](./01-overview/FILE_STRUCTURE.md) | Cần hiểu cấu trúc file của project |
| [diagrams_explained.md](./01-overview/diagrams_explained.md) | Cần hiểu ý nghĩa các sơ đồ |

## 02. Lộ trình học

| Thứ tự | Tài liệu |
|---:|---|
| 0 | [Mục lục lộ trình](./02-learning/00_INDEX.md) |
| 1 | [Ngôn ngữ C# cần biết](./02-learning/01_ngon_ngu_csharp_can_biet.md) |
| 2 | [WPF, XAML và MVVM cần biết](./02-learning/02_wpf_xaml_mvvm_can_biet.md) |
| 3 | [EF Core và SQL Server cần biết](./02-learning/03_ef_core_sql_server_can_biet.md) |
| 4 | [Kiến trúc dự án WarePro](./02-learning/04_kien_truc_du_an_quan_ly_hang_hoa.md) |
| 5 | [Nghiệp vụ chính và thuật toán](./02-learning/05_nghiep_vu_chinh_va_thuat_toan.md) |
| 6 | [Tự code lại dự án từ đầu](./02-learning/06_tu_code_lai_du_an_tu_dau.md) |
| 7 | [Bài tập theo tuần](./02-learning/07_bai_tap_theo_tuan.md) |
| 8 | [Thuật ngữ và phỏng vấn bảo vệ](./02-learning/08_bang_thuat_ngu_va_phong_van_bao_ve.md) |
| 9 | [Học C# bằng code từ số 0](./02-learning/09_csharp_bang_code_tu_so_0.md) |
| 10 | [Học WPF/MVVM bằng code từ số 0](./02-learning/10_wpf_mvvm_bang_code_tu_so_0.md) |
| 11 | [Học EF Core/SQL bằng code từ số 0](./02-learning/11_efcore_sql_bang_code_tu_so_0.md) |
| 12 | [Workbook tự code app mini](./02-learning/12_workbook_tu_code_app_mini.md) |

## 03. Giải thích mã nguồn

| Tài liệu | Phạm vi |
|---|---|
| [01 — Khởi động và điều hướng](./03-code/01_khoi_dong_va_dieu_huong.md) | Startup, login, shell và navigation |
| [02 — Models và database](./03-code/02_models_va_database.md) | Entity, field và quan hệ dữ liệu |
| [03 — Services nghiệp vụ](./03-code/03_services_nghiep_vu.md) | Pattern service và cách gọi dữ liệu |
| [04 — ViewModels](./03-code/04_viewmodels_logic_giaodien.md) | State, command, validation và binding |
| [05 — Views và inventory layer](./03-code/05_views_va_inventory_layer.md) | XAML, code-behind và inventory ports |
| [AuthenticationService](./03-code/authentication_service.md) | Xác thực và phiên người dùng |
| [Database và domain models](./03-code/database_and_domain_models.md) | Mapping dữ liệu chuyên sâu |
| [Business services chi tiết](./03-code/business_services_detailed.md) | Logic các service cốt lõi |
| [Presentation và ViewModels](./03-code/presentation_and_viewmodels.md) | Tầng presentation chuyên sâu |
| [Startup và DbContext dễ hiểu](./03-code/13_startup_dbcontext_mapping_de_hieu.md) | App startup, DbSet, mapping và seed |

## 04. Nghiệp vụ và luồng dữ liệu

| Tài liệu | Nghiệp vụ |
|---|---|
| [Execution và data flows](./04-business/execution_and_data_flows.md) | Luồng xuyên tầng của hệ thống |
| [Inventory core engine](./04-business/inventory_core_engine.md) | Balance, serial, ledger và posting |
| [Nhập/xuất kho dễ hiểu](./04-business/10_stockin_stockout_posting_de_hieu.md) | Draft, post, serial và transaction |
| [Kiểm kê, chuyển và điều chỉnh](./04-business/06_kiem_ke_chuyen_kho_dieu_chinh.md) | Nghiệp vụ kho nâng cao |
| [Opening balance import](./04-business/opening_balance_import.md) | Tồn đầu kỳ và import |
| [Hóa đơn, dashboard và báo cáo](./04-business/07_hoa_don_dashboard_bao_cao.md) | Thương mại, KPI và truy vết |
| [Audit, import và testing](./04-business/08_audit_import_testing.md) | Audit trail, nhập dữ liệu và test |
| [Report, audit và import dễ hiểu](./04-business/12_report_audit_import_de_hieu.md) | Các file dài được bóc nhỏ |
| [WarrantyClaimService](./04-business/warranty_claim_service.md) | Xử lý yêu cầu bảo hành |
| [Bảo hành và đổi serial dễ hiểu](./04-business/11_warranty_claim_doi_serial_de_hieu.md) | State transition và replacement |
| [Warranty tests](./04-business/warranty_tests.md) | Các trường hợp kiểm thử bảo hành |

## 05. Giao diện

| Tài liệu | Nội dung |
|---|---|
| [WarePro UI Design Guideline](./05-ui/warepro_ui_design_guideline.md) | Design system, layout, component và quy tắc WPF |
| [UI Typography Guideline](./05-ui/UI_TYPOGRAPHY_GUIDELINE.md) | Cỡ chữ, hierarchy và mật độ hiển thị |

## 06. Bài học và rà soát

| Tài liệu | Nội dung |
|---|---|
| [Reusable Engineering Playbook](./06-lessons/WAREPRO_REUSABLE_ENGINEERING_PLAYBOOK.md) | Kiến thức áp dụng cho dự án sau |
| [Deep Research Report](./06-lessons/deep-research-report.md) | Nghiên cứu và đánh giá dự án |
| [Documentation Coverage Audit](./06-lessons/DOC_COVERAGE_AUDIT.md) | Phần code đã/chưa được tài liệu hóa |
| [Sổ tay bảo vệ hội đồng](./06-lessons/09_so_tay_bao_ve_hoi_dong.md) | Câu hỏi và cách giải thích đồ án |

## Tài liệu ngoài Knowledge Hub

Các nội dung sau được liên kết nhưng có vòng đời riêng:

- [Diagram](../Diagram/README.md): Mermaid, PlantUML và SVG nguồn.
- [Project plans](../docs/project-plans/): kế hoạch sản phẩm cũ ở root.
- [Agent plans và specifications](../docs/superpowers/): tài liệu thực thi nội bộ.
- [Session handoffs](../docs/handoffs/): bàn giao phiên làm việc.
- [Diagnostics](../artifacts/diagnostics/): log, kiểm tra dữ liệu và đếm serial.
- [Word reports](../artifacts/documents/): tài liệu báo cáo, gồm cả bản local-only.

## Quy tắc duy trì

- Tài liệu giúp hiểu hoặc học dự án phải vào một trong sáu nhóm trên.
- Plan/spec/handoff không đưa vào thư viện kiến thức.
- Log, báo cáo sinh tự động và Word không đặt ở root.
- Diagram nguồn ở `Diagram/`; tài liệu giải thích diagram ở `01-overview/`.
- Khi di chuyển file, cập nhật `INDEX.md` và kiểm tra toàn bộ link tương đối.
