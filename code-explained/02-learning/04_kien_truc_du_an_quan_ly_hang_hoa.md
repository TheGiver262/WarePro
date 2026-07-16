# Kiến trúc dự án QuanLyHangHoa

Dự án là ứng dụng quản lý hàng hóa, kho, hóa đơn và bảo hành. Kiến trúc chính là layered architecture kết hợp MVVM.

## 1. Bức tranh tổng thể

```text
Views/XAML
-> ViewModels
-> Services
-> Models + Inventory domain
-> AppDbContext
-> SQL Server
```

Vai trò từng tầng:

- View: hiển thị và nhận thao tác người dùng.
- ViewModel: giữ trạng thái màn hình, command, validation UI.
- Service: xử lý nghiệp vụ ứng dụng.
- Inventory: quy tắc kho lõi, có thể test độc lập.
- Model: dữ liệu/entity.
- AppDbContext: map entity vào database.

## 2. Project chính

```text
QuanLyHangHoa/
```

Đây là app WPF chính. File project:

```text
QuanLyHangHoa/QuanLyHangHoa.csproj
```

Điểm quan trọng trong csproj:

- `TargetFramework`: `net8.0-windows`
- `UseWPF`: `true`
- `Nullable`: `enable`
- Thư viện: EF Core SQL Server, CommunityToolkit.Mvvm, ClosedXML, CsvHelper, LiveCharts, MaterialDesign.

## 3. Project test

```text
QuanLyHangHoa.Tests/
```

Chứa unit test và integration test. Đây là tài sản học rất quý vì test cho biết hệ thống kỳ vọng hành vi gì.

Các nhóm test:

- `Inventory/*`: test thuật toán kho.
- `Services/*`: test service nghiệp vụ.
- `ViewModels/*`: test logic màn hình.
- `Helpers/DatabaseHelper.cs`: hỗ trợ database test.

## 4. App startup

Điểm khởi động WPF là:

```text
QuanLyHangHoa/App.xaml
QuanLyHangHoa/App.xaml.cs
```

Luồng tổng quát:

1. App chạy.
2. Mở màn hình login.
3. Login thành công tạo `MainWindow`.
4. `MainWindow` tạo `MainViewModel`.
5. `MainViewModel` mở dashboard.

## 5. MainWindow và MainViewModel

`MainWindow.xaml.cs` rất mỏng:

```csharp
this.DataContext = new MainViewModel(user, contextFactory);
```

Nghĩa là cửa sổ chính giao quyền điều khiển cho `MainViewModel`.

`MainViewModel` làm 4 việc:

- Lưu `CurrentUser`.
- Lưu `CurrentView`.
- Điều hướng menu bằng command.
- Cache view bằng `_viewCache`.

Pattern điều hướng:

```text
OpenStockInViewCommand
-> OpenStockInView()
-> NavigateToView(...)
-> tạo StockInView + StockInViewModel
-> gán CurrentView
```

## 6. Views

Thư mục:

```text
QuanLyHangHoa/Views
```

Mỗi màn hình thường có:

- `ProductView.xaml`
- `ProductView.xaml.cs`
- `ProductViewModel.cs`

View chỉ nên biết UI. Logic nghiệp vụ phải nằm ở ViewModel hoặc Service.

## 7. ViewModels

Thư mục:

```text
QuanLyHangHoa/ViewModels
```

ViewModel thường:

- Kế thừa `ObservableObject`.
- Dùng `[ObservableProperty]` cho state.
- Dùng `[RelayCommand]` cho hành động.
- Gọi service để đọc/ghi dữ liệu.
- Không gọi SQL trực tiếp nếu có service phù hợp.

Ví dụ nhóm ViewModel chính:

- `LoginViewModel`
- `MainViewModel`
- `ProductViewModel`
- `StockInViewModel`
- `StockOutViewModel`
- `WarrantyClaimViewModel`
- `DashboardViewModel`

## 8. Services

Thư mục:

```text
QuanLyHangHoa/Services
```

Service chứa nghiệp vụ cấp ứng dụng:

- `AuthenticationService`: đăng nhập, đổi mật khẩu.
- `AuthorizationService`: phân quyền.
- `ProductService`: CRUD sản phẩm.
- `StockInService`: lưu nháp và ghi sổ nhập kho.
- `StockOutService`: xuất kho.
- `StockTransferService`: chuyển kho.
- `InvoiceService`: hóa đơn.
- `WarrantyClaimService`: hồ sơ bảo hành.
- `OpeningBalanceImportService`: nhập tồn đầu kỳ.

Service thường nhận `Func<AppDbContext>` để tạo context ngắn hạn.

## 9. Inventory domain

Thư mục:

```text
QuanLyHangHoa/Inventory
```

Đây là lõi thuật toán kho. File cần học kỹ nhất:

```text
InventoryPostingService.cs
```

Nó xử lý:

- Ghi sổ nhập kho.
- Ghi sổ xuất kho.
- Ghi sổ chuyển kho.
- Kiểm tra serial.
- Cập nhật tồn kho.
- Tạo stock ledger.
- Tạo audit log.

Điểm đáng học: tầng này dùng interface như `IInventoryUnitOfWork`, nên test được mà không cần UI.

## 10. Models

Thư mục:

```text
QuanLyHangHoa/Models
```

Model là entity dữ liệu:

- Danh mục: `Product`, `Category`, `Brand`, `Unit`, `Warehouse`.
- Kho: `StockIn`, `StockOut`, `StockBalance`, `StockLedger`, `ProductSerial`.
- Hóa đơn: `PurchaseInvoice`, `SalesInvoice`.
- Bảo hành: `WarrantyCoverage`, `WarrantyClaim`.
- Hệ thống: `AppUser`, `AuditLog`.

Khi muốn biết một bảng có field nào, đọc model trước, rồi đọc mapping trong `AppDbContext`.

## 11. Data

Thư mục:

```text
QuanLyHangHoa/Data
```

File chính:

```text
AppDbContext.cs
```

Đây là nơi khai báo database schema theo EF Core.

Khi gặp lỗi database hoặc muốn thêm bảng, đây là file phải hiểu.

## 12. Themes

Thư mục:

```text
QuanLyHangHoa/Themes
```

Chứa style chung. Khi sửa UI:

- Không hard-code màu lung tung.
- Ưu tiên dùng resource.
- Tuân thủ guideline của repo: Pro Max, glassmorphism, không dùng purple/violet chuẩn.

## 13. Import data

Thư mục:

```text
QuanLyHangHoa/Services/DataImport
```

Các lớp chính:

- `ExcelImportService`
- `CsvImportService`
- `DynamicImportService`
- `FileClassificationService`
- `DataImportManager`
- `DatabaseSeeder`

Nhóm này xử lý đọc dữ liệu Excel/CSV và map vào model.

## 14. Tài liệu có sẵn nên tận dụng

Dự án đã có thư mục:

```text
code-explained/
```

Đây là tài liệu giải thích kiến trúc trong bộ học có lộ trình. Khi đã nắm phần này, đọc tiếp [mục lục code-explained](../INDEX.md) để chuyển sang các chương code và nghiệp vụ chi tiết.

## 15. Cách tự trace một chức năng

Ví dụ trace chức năng nhập kho:

1. Mở `StockInView.xaml`: tìm nút ghi sổ.
2. Tìm command binding trong XAML.
3. Mở `StockInViewModel.cs`: tìm method command đó.
4. Xem ViewModel gọi `StockInService` hàm nào.
5. Mở `StockInService.cs`: đọc validation và transaction.
6. Xem nó gọi `InventoryPostingService` thế nào.
7. Mở test `PostStockInTests.cs` để hiểu case đúng/sai.

Đọc code theo luồng như vậy nhanh hơn đọc từng thư mục từ trên xuống.
