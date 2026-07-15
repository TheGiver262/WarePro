# Roadmap tự code lại dự án từ đầu

Mục tiêu không phải copy lại toàn bộ app ngay. Mục tiêu là tự xây một phiên bản nhỏ, đúng kiến trúc, rồi mở rộng dần đến gần dự án thật.

## Giai đoạn 0: Chuẩn bị tư duy

Bạn cần nắm 3 nguyên tắc:

1. UI không xử lý nghiệp vụ.
2. Service không phụ thuộc View.
3. Nghiệp vụ kho phải có transaction và test.

Nếu giữ 3 nguyên tắc này, app sẽ dễ mở rộng hơn rất nhiều.

## Giai đoạn 1: Tạo solution

Tạo:

```text
WarehouseMini/
WarehouseMini.Tests/
```

Công nghệ:

- .NET 8 WPF
- CommunityToolkit.Mvvm
- EF Core SQL Server
- xUnit/NUnit/MSTest tùy bạn chọn, nhưng nên thống nhất một loại

Mục tiêu:

- App chạy được.
- Có `MainWindow`.
- Có `AppDbContext`.
- Có project test chạy được.

## Giai đoạn 2: Login đơn giản

Tạo model:

- `AppUser`

Field tối thiểu:

- `Id`
- `Username`
- `PasswordHash`
- `FullName`
- `RoleCode`
- `IsActive`
- `FailedLoginCount`
- `LockoutUntil`

Tạo:

- `AuthenticationService`
- `LoginViewModel`
- `LoginView`

Yêu cầu:

- Hash password bằng BCrypt.
- Sai mật khẩu tăng failed count.
- User inactive không đăng nhập được.
- Login đúng mở `MainWindow`.

## Giai đoạn 3: Navigation shell

Tạo:

- `MainViewModel`
- `DashboardView`
- `ProductView`
- `StockInView`
- `StockOutView`

Mục tiêu:

- Sidebar/menu đổi `CurrentView`.
- Mỗi view có ViewModel riêng.
- Không viết nghiệp vụ trong code-behind.

Pattern:

```text
Button menu
-> OpenProductViewCommand
-> MainViewModel tạo ProductView + ProductViewModel
-> CurrentView đổi
```

## Giai đoạn 4: Danh mục sản phẩm

Tạo model:

- `Category`
- `Brand`
- `Unit`
- `Product`
- `Warehouse`

Tạo service:

- `ProductService`
- `CategoryService`
- `BrandService`
- `UnitService`

Tạo UI:

- Danh sách sản phẩm.
- Thêm/sửa sản phẩm.
- Tìm kiếm.
- Xóa mềm.

Yêu cầu:

- `ProductCode` unique.
- Không cho lưu thiếu tên.
- Không cho giá âm.

## Giai đoạn 5: Tồn kho cơ bản

Tạo model:

- `StockBalance`
- `StockLedger`

Ý tưởng:

- `StockBalance` giữ số dư hiện tại theo sản phẩm/kho.
- `StockLedger` ghi lịch sử biến động.

Tạo service:

- `InventoryPostingService`

Hàm đầu tiên:

```csharp
PostStockIn(productId, warehouseId, quantity, userId)
```

Yêu cầu:

- Quantity > 0.
- Tăng balance.
- Ghi ledger IN.
- Có test.

## Giai đoạn 6: Nhập kho có phiếu

Tạo model:

- `StockIn`
- `StockInLine`

Trạng thái:

- `Draft`
- `Approved`
- `Posted`
- `Cancelled`

Luồng:

```text
Tạo phiếu Draft
-> Thêm dòng sản phẩm
-> SaveDraft
-> Post
-> InventoryPostingService tăng tồn
-> Đổi phiếu thành Posted
```

Yêu cầu:

- Không sửa phiếu đã posted.
- Không ghi sổ phiếu không có dòng.
- Ghi sổ trong transaction.

## Giai đoạn 7: Xuất kho

Tạo model:

- `StockOut`
- `StockOutLine`

Hàm:

```csharp
PostStockOut(productId, warehouseId, quantity, userId)
```

Yêu cầu:

- Quantity > 0.
- Tồn khả dụng đủ.
- Giảm balance.
- Ghi ledger OUT.
- Không cho tồn âm.
- Có test case thiếu tồn.

## Giai đoạn 8: Serial

Tạo model:

- `ProductSerial`

Field:

- `Id`
- `SerialNumber`
- `ProductId`
- `CurrentWarehouseId`
- `CurrentStatus`

Mở rộng nhập kho:

- Sản phẩm `IsSerialTracked = true` thì số serial phải bằng số lượng.
- Serial nhập vào không được trùng.

Mở rộng xuất kho:

- Serial phải tồn tại.
- Serial phải thuộc đúng sản phẩm.
- Serial phải ở đúng kho.
- Serial phải đang `InStock`.

## Giai đoạn 9: Chuyển kho

Tạo:

- `StockTransfer`
- `StockTransferLine`

Luồng:

```text
Validate kho nguồn khác kho đích
-> Giảm balance kho nguồn
-> Tăng balance kho đích
-> Cập nhật serial sang kho đích
-> Ghi ledger OUT và IN
```

Test bắt buộc:

- Chuyển quá tồn bị chặn.
- Chuyển cùng kho bị chặn.
- Serial ở kho khác bị chặn.

## Giai đoạn 10: Hóa đơn

Tạo:

- `PurchaseInvoice`
- `PurchaseInvoiceLine`
- `SalesInvoice`
- `SalesInvoiceLine`

Tối thiểu:

- Tạo hóa đơn.
- Tính tổng tiền.
- Theo dõi thanh toán.
- Gắn hóa đơn bán với phiếu xuất.

Chưa cần làm kế toán phức tạp. Đúng dữ liệu trước, đẹp UI sau.

## Giai đoạn 11: Bảo hành

Tạo:

- `WarrantyCoverage`
- `WarrantyClaim`

Luồng tối thiểu:

```text
Sau bán hàng tạo coverage
-> Khách yêu cầu bảo hành
-> Tạo claim
-> Cập nhật trạng thái xử lý
-> Đóng claim
```

Sau đó mới thêm đổi serial, gửi hãng, ngày hẹn trả.

## Giai đoạn 12: Import Excel/CSV

Bắt đầu với CSV:

1. Đọc file.
2. Parse từng dòng.
3. Validate.
4. Hiển thị lỗi.
5. Import nếu không lỗi.

Sau khi CSV ổn, thêm Excel bằng ClosedXML.

## Giai đoạn 13: Dashboard và báo cáo

Chỉ làm sau khi dữ liệu đã đúng.

Dashboard nên đọc từ:

- `StockBalance`
- `StockLedger`
- `SalesInvoice`
- `PurchaseInvoice`
- `WarrantyClaim`

Không tính dashboard bằng dữ liệu hard-code.

## Giai đoạn 14: Hoàn thiện

Checklist:

- Build sạch.
- Test nghiệp vụ kho.
- Test authentication.
- UI không crash khi dữ liệu rỗng.
- Có audit log cho nghiệp vụ quan trọng.
- Không có code nghiệp vụ lớn trong View.

## Thứ tự code khuyến nghị

1. Model
2. DbContext mapping
3. Service
4. Test service
5. ViewModel
6. View
7. Manual test UI

Đừng bắt đầu bằng UI phức tạp. UI đẹp nhưng service sai thì app vẫn sai.
