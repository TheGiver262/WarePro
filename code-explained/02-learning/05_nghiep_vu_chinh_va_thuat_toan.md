# Nghiệp vụ chính và thuật toán

Tài liệu này giải thích các luồng nghiệp vụ theo kiểu senior developer nhìn hệ thống: dữ liệu nào đi vào, kiểm tra gì, cập nhật bảng nào, lỗi nào phải chặn.

## 1. Đăng nhập

File chính:

- `Services/AuthenticationService.cs`
- `ViewModels/LoginViewModel.cs`
- `Views/LoginView.xaml`
- `Models/AppUser.cs`

Luồng:

```text
User nhập username/password
-> LoginViewModel gọi AuthenticationService.Authenticate
-> Service tìm AppUser theo username
-> Kiểm tra đúng chữ hoa/thường
-> Kiểm tra active
-> Kiểm tra lockout
-> Verify password bằng BCrypt
-> Nếu đúng: cập nhật LastLoginAt, reset FailedLoginCount
-> Nếu sai: tăng FailedLoginCount, có thể khóa tài khoản
```

Thuật toán đáng nhớ:

- Password không lưu plain text, lưu hash BCrypt.
- Sai nhiều lần thì khóa tạm thời.
- Có audit log cho các lần login thất bại.

## 2. Phân quyền

File chính:

- `Services/AuthorizationService.cs`
- `Models/AppUser.cs`
- `ViewModels/MainViewModel.cs`

Ý tưởng:

- User có `RoleCode`.
- App kiểm tra role có được thực hiện action không.
- UI chỉ mở màn hình admin nếu user đủ quyền.

Ví dụ trong `MainViewModel`:

```csharp
public bool IsAdmin => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageUsers);
```

Khi tự code lại, hãy làm phân quyền đơn giản trước: Admin và Staff.

## 3. CRUD danh mục

Áp dụng cho:

- Sản phẩm
- Nhóm sản phẩm
- Thương hiệu
- Đơn vị
- Nhà cung cấp
- Khách hàng
- Kho

Luồng chuẩn:

```text
View hiển thị danh sách
-> ViewModel LoadData
-> Service query database
-> User thêm/sửa/xóa
-> ViewModel validate dữ liệu cơ bản
-> Service validate nghiệp vụ
-> SaveChanges
-> Reload list
```

Quy tắc quan trọng:

- Mã không được trùng.
- Không xóa cứng dữ liệu đã phát sinh giao dịch.
- Ưu tiên `IsActive = false` để xóa mềm.

## 4. Nhập kho

File chính:

- `StockInView.xaml`
- `StockInViewModel.cs`
- `StockInService.cs`
- `InventoryPostingService.cs`
- `StockIn`, `StockInLine`, `StockBalance`, `ProductSerial`, `StockLedger`

Luồng ghi sổ nhập kho:

```text
User tạo phiếu nhập
-> Chọn kho, nhà cung cấp, sản phẩm, số lượng
-> Nếu sản phẩm quản lý serial thì nhập serial
-> Lưu nháp
-> Ghi sổ
-> Validate phiếu
-> Validate serial
-> Tăng StockBalance
-> Tạo ProductSerial nếu có serial
-> Ghi StockLedger hướng IN
-> Ghi AuditLog
-> Đổi trạng thái phiếu thành Posted
```

Thuật toán trong `PostStockIn`:

1. Chỉ cho ghi sổ nếu trạng thái hợp lệ.
2. Số lượng phải lớn hơn 0.
3. Trim serial và bỏ serial rỗng.
4. Chặn serial trùng trong cùng phiếu.
5. Nếu sản phẩm quản lý serial, số serial phải bằng số lượng.
6. Nếu sản phẩm không quản lý serial, không được nhập serial.
7. Chặn serial đã tồn tại trong database.
8. Tăng `OnHandQuantity` và `AvailableQuantity`.
9. Tạo serial trạng thái `InStock`.
10. Thêm ledger.
11. Thêm audit.
12. Commit.

## 5. Xuất kho

File chính:

- `StockOutView.xaml`
- `StockOutViewModel.cs`
- `StockOutService.cs`
- `InventoryPostingService.cs`

Luồng:

```text
User tạo phiếu xuất
-> Chọn kho, khách hàng, sản phẩm, số lượng
-> Chọn serial nếu cần
-> Ghi sổ
-> Kiểm tra tồn khả dụng
-> Kiểm tra serial thuộc đúng sản phẩm/kho
-> Giảm StockBalance
-> Đổi serial thành Sold
-> Ghi StockLedger hướng OUT
-> Ghi AuditLog
```

Điểm quan trọng:

- Không cho xuất quá tồn.
- Không cho xuất serial ở kho khác.
- Không cho xuất serial đã bán hoặc không còn `InStock`.

## 6. Chuyển kho

File chính:

- `StockTransferService.cs`
- `InventoryPostingService.PostStockTransfer`
- `StockTransfer`, `StockTransferLine`

Luồng:

```text
Chọn kho nguồn
-> Chọn kho đích
-> Chọn sản phẩm/số lượng/serial
-> Kiểm tra kho nguồn khác kho đích
-> Kiểm tra tồn kho nguồn
-> Giảm tồn kho nguồn
-> Tăng tồn kho đích
-> Cập nhật CurrentWarehouseId của serial
-> Ghi 2 ledger: OUT ở kho nguồn, IN ở kho đích
```

Đây là nghiệp vụ hay bị sai nếu chỉ ghi một dòng ledger. Chuyển kho phải nhìn như một cặp xuất/nhập.

## 7. Kiểm kê và điều chỉnh

File chính:

- `StockCountService.cs`
- `StockAdjustmentService.cs`
- `InventoryAdjustmentService.cs`
- `StockCountSession`, `StockCountLine`
- `StockAdjustment`, `StockAdjustmentLine`

Ý tưởng:

- Kiểm kê ghi nhận số lượng thực tế.
- Hệ thống so sánh với tồn sổ sách.
- Nếu lệch, tạo điều chỉnh tăng/giảm.

Thuật toán:

```text
BookQuantity = tồn hệ thống
CountedQuantity = tồn thực tế
Difference = CountedQuantity - BookQuantity
Nếu Difference > 0: điều chỉnh tăng
Nếu Difference < 0: điều chỉnh giảm
Nếu Difference = 0: không cần điều chỉnh
```

## 8. Hóa đơn mua/bán

File chính:

- `InvoiceService.cs`
- `PurchaseInvoiceViewModel.cs`
- `SalesInvoiceViewModel.cs`
- `PurchaseInvoice`, `PurchaseInvoiceLine`
- `SalesInvoice`, `SalesInvoiceLine`

Ý tưởng:

- Hóa đơn mua có thể gắn với phiếu nhập.
- Hóa đơn bán có thể gắn với phiếu xuất.
- Dòng hóa đơn tính tiền theo số lượng, đơn giá, thuế.

Công thức:

```text
SubTotal = Quantity * UnitPrice
TaxAmount = SubTotal * TaxRate
GrandTotal = SubTotal + TaxAmount
PaymentStatus dựa trên PaidAmount và GrandTotal
```

## 9. Bảo hành

File chính:

- `WarrantyCoverage`
- `WarrantyClaim`
- `WarrantyService.cs`
- `WarrantyClaimService.cs`
- `WarrantyViewModel.cs`

Khái niệm:

- `WarrantyCoverage`: quyền bảo hành của một serial sau khi bán.
- `WarrantyClaim`: hồ sơ xử lý một lần yêu cầu bảo hành.

Luồng:

```text
Khách mua hàng
-> Serial được bán
-> Tạo WarrantyCoverage
-> Khách báo lỗi
-> Tạo WarrantyClaim
-> Tiếp nhận
-> Xử lý kỹ thuật
-> Có thể đổi serial mới
-> Đóng hồ sơ
```

Quy tắc:

- Một serial chỉ nên có một coverage active.
- Không mở nhiều claim active cho cùng một serial nếu chưa đóng hồ sơ cũ.
- Đổi mới có thể phát sinh phiếu xuất bảo hành.

## 10. Import Excel/CSV

File chính:

- `Services/DataImport/ExcelImportService.cs`
- `Services/DataImport/CsvImportService.cs`
- `OpeningBalanceImportService.cs`
- `OpeningBalanceImportViewModel.cs`

Luồng:

```text
User chọn file
-> Phân loại file
-> Đọc Excel/CSV
-> Map cột vào model tạm
-> Validate từng dòng
-> Báo lỗi dòng sai
-> Nếu hợp lệ thì import vào database
```

Khi tự code lại, hãy bắt đầu bằng CSV vì dễ hơn Excel.

## 11. Audit log

Audit log trả lời câu hỏi: ai đã làm gì, lúc nào, trên dữ liệu nào.

File/table:

- `AuditLog`
- `AuditLogService.cs`
- `AuditLogViewModel.cs`

Nghiệp vụ quan trọng nên ghi audit:

- Đăng nhập thất bại.
- Tạo/sửa/xóa danh mục.
- Ghi sổ nhập/xuất/chuyển kho.
- Đổi mật khẩu.
- Xử lý bảo hành.

## 12. Stock ledger

`StockLedger` là sổ nhật ký kho. `StockBalance` là số dư hiện tại.

Khác nhau:

- `StockBalance`: hiện còn bao nhiêu.
- `StockLedger`: vì sao số lượng thay đổi.

Nếu chỉ có balance, bạn biết còn 10 cái nhưng không biết lịch sử. Nếu có ledger, bạn truy vết được từng nhập/xuất.

## 13. Bài tập thuật toán

1. Viết pseudo-code nhập kho cho sản phẩm không quản lý serial.
2. Viết pseudo-code nhập kho cho sản phẩm có serial.
3. Viết pseudo-code xuất kho và chặn tồn âm.
4. Viết pseudo-code chuyển kho với 2 ledger.
5. Viết 5 test case cho đăng nhập sai mật khẩu.

## 14. Mốc đạt yêu cầu

Bạn hiểu nghiệp vụ khi có thể giải thích:

- Vì sao nhập kho phải tăng balance và ghi ledger.
- Vì sao xuất kho phải kiểm tra available.
- Vì sao serial phải unique.
- Vì sao cần transaction.
- Vì sao audit log không thay thế stock ledger.
