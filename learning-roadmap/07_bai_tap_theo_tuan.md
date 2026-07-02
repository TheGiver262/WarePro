# Lịch học 8 tuần

Lịch này thiết kế để bạn vừa học nền tảng, vừa đọc dự án, vừa tự code lại bản mini.

## Tuần 1: C# căn bản trong ngữ cảnh dự án

Mục tiêu:

- Đọc được class, property, constructor, collection, LINQ cơ bản.

Đọc:

- `learning-roadmap/01_ngon_ngu_csharp_can_biet.md`
- `QuanLyHangHoa/Models/Product.cs`
- `QuanLyHangHoa/Models/AppUser.cs`
- `QuanLyHangHoa/Services/AuthenticationService.cs`

Bài tập:

1. Viết class `Student`, `Course`, `Enrollment`.
2. Tạo list student và lọc bằng LINQ.
3. Giải thích bằng lời hàm `Authenticate`.

Tự chấm:

- Bạn giải thích được `FirstOrDefault`, `Where`, `Select`.
- Bạn biết property nào có thể null.
- Bạn hiểu `using var db`.

## Tuần 2: WPF và MVVM

Mục tiêu:

- Hiểu View, ViewModel, Binding, Command.

Đọc:

- `learning-roadmap/02_wpf_xaml_mvvm_can_biet.md`
- `QuanLyHangHoa/MainWindow.xaml.cs`
- `QuanLyHangHoa/ViewModels/MainViewModel.cs`
- Một file `Views/*.xaml` tùy chọn.

Bài tập:

1. Tạo app WPF mini có `MainWindow`.
2. Tạo `MainViewModel` có `Title`.
3. Binding `Title` lên UI.
4. Tạo nút đổi title bằng `[RelayCommand]`.

Tự chấm:

- Bạn biết DataContext là gì.
- Bạn biết nút gọi command nào.
- Bạn không viết logic nghiệp vụ trong code-behind.

## Tuần 3: EF Core và database

Mục tiêu:

- Tạo model, DbContext, query, save.

Đọc:

- `learning-roadmap/03_ef_core_sql_server_can_biet.md`
- `QuanLyHangHoa/Data/AppDbContext.cs`
- `QuanLyHangHoa/Models/Product.cs`
- `QuanLyHangHoa/Models/StockBalance.cs`

Bài tập:

1. Tạo database mini.
2. Tạo entity `Product`.
3. Thêm/sửa/xóa mềm sản phẩm.
4. Query sản phẩm active.

Tự chấm:

- Bạn biết `DbSet` là gì.
- Bạn biết `SaveChanges` làm gì.
- Bạn hiểu unique index.

## Tuần 4: CRUD danh mục

Mục tiêu:

- Tự làm một màn hình CRUD hoàn chỉnh.

Đọc:

- `ProductService.cs`
- `ProductViewModel.cs`
- `ProductView.xaml`
- Các service danh mục như `BrandService`, `CategoryService`.

Bài tập:

1. Tự làm CRUD `Warehouse`.
2. Có tìm kiếm.
3. Có xóa mềm.
4. Có validate mã không rỗng.

Tự chấm:

- ViewModel gọi service, không gọi SQL trực tiếp nếu đã có service.
- Service chặn mã trùng.
- UI reload sau khi lưu.

## Tuần 5: Nhập kho

Mục tiêu:

- Hiểu ghi sổ nhập kho và stock balance.

Đọc:

- `learning-roadmap/05_nghiep_vu_chinh_va_thuat_toan.md`
- `Inventory/InventoryPostingService.cs`
- `Services/StockInService.cs`
- `ViewModels/StockInViewModel.cs`
- `QuanLyHangHoa.Tests/Inventory/PostStockInTests.cs`

Bài tập:

1. Viết service `PostStockIn`.
2. Tăng `StockBalance`.
3. Ghi `StockLedger`.
4. Test số lượng âm bị chặn.
5. Test nhập thành công tăng tồn.

Tự chấm:

- Có transaction hoặc unit-of-work rõ ràng.
- Không cho quantity <= 0.
- Ledger khớp với balance.

## Tuần 6: Xuất kho và serial

Mục tiêu:

- Chặn tồn âm và hiểu serial tracking.

Đọc:

- `InventoryPostingService.PostStockOut`
- `Models/ProductSerial.cs`
- `Services/ProductSerialService.cs`
- `QuanLyHangHoa.Tests/Inventory/PostStockOutTests.cs`
- `QuanLyHangHoa.Tests/Inventory/SerialValidationTests.cs`

Bài tập:

1. Viết `PostStockOut`.
2. Nếu thiếu tồn thì throw exception.
3. Nếu sản phẩm serial-tracked thì bắt nhập đủ serial.
4. Khi xuất serial, đổi status sang `Sold`.

Tự chấm:

- Không thể xuất serial ở kho khác.
- Không thể xuất serial không thuộc sản phẩm.
- Không thể xuất quá tồn.

## Tuần 7: Hóa đơn và bảo hành

Mục tiêu:

- Hiểu phần nghiệp vụ mở rộng sau kho.

Đọc:

- `InvoiceService.cs`
- `WarrantyClaimService.cs`
- `WarrantyService.cs`
- `Models/WarrantyCoverage.cs`
- `Models/WarrantyClaim.cs`
- `code-explained/warranty_claim_service.md`

Bài tập:

1. Tạo hóa đơn bán từ phiếu xuất.
2. Tính `SubTotal`, `TaxAmount`, `GrandTotal`.
3. Tạo coverage bảo hành cho serial đã bán.
4. Tạo claim bảo hành đơn giản.

Tự chấm:

- Tổng tiền tính đúng.
- Claim gắn đúng serial.
- Không mở claim mới nếu claim cũ chưa đóng.

## Tuần 8: Tự code mini project end-to-end

Mục tiêu:

- Có bản mini chạy được từ login đến nhập/xuất kho.

Yêu cầu tối thiểu:

- Login.
- Dashboard trống hoặc đơn giản.
- CRUD sản phẩm.
- CRUD kho.
- Nhập kho.
- Xuất kho.
- Tồn kho.
- Stock ledger.
- Unit tests cho nhập/xuất.

Bài bảo vệ thử:

1. App dùng kiến trúc gì?
2. Vì sao dùng MVVM?
3. Vì sao cần `StockLedger` nếu đã có `StockBalance`?
4. Vì sao cần transaction khi ghi sổ?
5. Nếu mất điện giữa lúc ghi phiếu thì hệ thống bảo vệ dữ liệu thế nào?

Tự chấm:

- Build thành công.
- Test pass.
- Bạn demo được luồng nhập rồi xuất.
- Bạn giải thích được từng bảng bị ảnh hưởng.

## Sau 8 tuần

Khi đã xong bản mini, quay lại repo thật và chọn một module để đọc sâu:

1. Inventory.
2. Warranty.
3. Import Excel/CSV.
4. Dashboard/report.
5. UI/theme.

Đọc sâu theo module sẽ hiệu quả hơn cố đọc toàn bộ repo cùng lúc.
