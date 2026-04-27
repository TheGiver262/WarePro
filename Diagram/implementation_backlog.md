# Backlog triển khai từ bộ thiết kế Diagram

Tài liệu này dùng để chuyển bộ design trong `Diagram` thành backlog triển khai theo module. Đây là backlog mức hệ thống, không phải checklist code chi tiết theo file.

## 1. Mục tiêu triển khai
- Bám theo target baseline trong `Thiết kế phần mềm.txt`.
- Không lấy code hiện tại làm giới hạn kiến trúc.
- Ưu tiên hoàn thành các khối `must-have` trước khi làm báo cáo nâng cao hoặc mở rộng tài chính.

## 2. Must-have for implementation
### 2.1 Auth và phân quyền
- AppUser với `RoleCode` cố định.
- Đăng nhập, đổi mật khẩu, kiểm tra quyền theo màn hình và hành động.
- Audit các thao tác quản trị người dùng.

### 2.2 Danh mục nền
- Category, Brand, Unit, Supplier, Customer.
- Soft-delete hoặc `IsActive`, không xóa cứng bản ghi đã phát sinh giao dịch.

### 2.3 Sản phẩm và serial
- Product, ProductUnit, ProductSerial.
- Quản lý hàng có serial và không serial trong cùng domain.
- Ràng buộc serial duy nhất toàn hệ thống.

### 2.4 Kho và chứng từ kho
- StockBalance là nguồn chuẩn cho tồn hiện tại.
- StockLedger là nguồn chuẩn cho lịch sử kho.
- StockIn, StockOut, StockCountSession, StockAdjustment.
- Tách rõ `Lập`, `Duyệt`, `Ghi sổ`.

### 2.5 Hóa đơn và công nợ
- PurchaseInvoice và SalesInvoice.
- Theo dõi `TotalAmount`, `PaidAmount`, `PaymentStatus`, `DueDate`.
- Chưa triển khai bảng `InvoicePayment`.

### 2.6 Bảo hành
- WarrantyCoverage và WarrantyClaim.
- Từ chối bảo hành phải có lý do và luồng trả máy cho khách.
- Đổi mới phải ghi `ReplacementSerialId` và coverage kế thừa thời hạn còn lại.

### 2.7 Audit
- AuditLog cho thay đổi nghiệp vụ ngoài kho.
- StockLedger cho audit chuyên biệt của kho.

## 3. Thứ tự module đề xuất
1. Auth và phân quyền.
2. Danh mục nền.
3. Sản phẩm, đơn vị, serial.
4. Nhập kho.
5. Xuất kho.
6. Kiểm kê, điều chỉnh và đảo nghiệp vụ.
7. Hóa đơn và công nợ.
8. Bảo hành.
9. Báo cáo và audit viewer.

## 4. Acceptance scenarios bắt buộc
- Nhập kho hàng có serial.
- Nhập kho hàng không serial.
- Xuất kho khi đủ tồn.
- Xuất kho khi thiếu tồn.
- Kiểm kê phát hiện chênh lệch và sinh chứng từ điều chỉnh.
- Đảo nghiệp vụ sau khi chứng từ đã ghi sổ.
- Bảo hành sửa nội bộ.
- Bảo hành gửi hãng sửa được.
- Bảo hành đổi mới.
- Bảo hành bị từ chối và trả lại máy.

## 5. Optional for phase 2
- Workflow giữ chỗ hoặc đặt hàng.
- Thanh toán nhiều lần cho một hóa đơn.
- Báo cáo đa chiều và dashboard quản trị nâng cao.
- Tự động hóa nhiều cấp duyệt.

## 6. Future enhancement
- Role/Permission động thay cho `RoleCode` cố định.
- Tích hợp cổng thanh toán hoặc đối soát công nợ tự động.
- Tích hợp hãng bảo hành qua API.
