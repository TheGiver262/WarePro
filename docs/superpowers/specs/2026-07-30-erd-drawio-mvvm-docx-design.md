# Thiết kế ERD Draw.io và bổ sung hình MVVM cho báo cáo

## Mục tiêu

- Tạo lại bộ ERD của WarePro dựa trên quan hệ thật trong `AppDbContext`.
- Tạo một ERD tổng quan chia thành sáu phân hệ, thể hiện rõ quan hệ nội bộ và liên phân hệ.
- Sửa sáu ERD chi tiết, bảo đảm đúng khóa ngoại và không bỏ sót quan hệ liên phân hệ quan trọng.
- Gom toàn bộ bảy ERD vào một file Draw.io nhiều trang và xuất từng trang thành PNG.
- Chỉ sửa DOCX mới nhất để thêm hình MVVM, đánh lại số hình và cập nhật các danh mục cùng số trang.

## Phạm vi đầu ra

### Draw.io và ảnh ERD

Tạo mới trong `C:\Users\player\Desktop\DATN\final`:

- `WarePro_ERD_Tong_20260730.drawio`: file Draw.io tổng gồm bảy trang.
- Thư mục `ERD_WarePro_20260730`: chứa bảy PNG xuất từ bảy trang Draw.io.

Bảy trang gồm:

1. `ERD tổng quan`
2. `Danh mục và đối tác`
3. `Nhập xuất kho và số dư`
4. `Điều chuyển kiểm kê và số sê-ri`
5. `Hóa đơn`
6. `Bảo hành`
7. `Người dùng và nhật ký`

### DOCX

Sửa trực tiếp:

`C:\Users\player\Desktop\DATN\final\DA_NguyenCongMinh_TKPMQuanLyKhovaBaoHanh_CHI_TIET_CSDL_20260730.docx`

Trước khi sửa, sao lưu bản hiện tại vào thư mục làm việc tạm. Không thay các ERD trong DOCX; người dùng sẽ tự chèn ảnh ERD sau.

## Thiết kế ERD tổng quan

ERD tổng quan dùng bố cục ngang, chia sáu khung phân hệ:

- Hàng trên: `Danh mục và đối tác` — `Nhập/xuất kho và số dư` — `Hóa đơn`.
- Hàng dưới: `Người dùng và nhật ký` — `Điều chuyển, kiểm kê và số sê-ri` — `Bảo hành`.

Mỗi khung chỉ chứa các ô ghi tên bảng, không hiển thị thuộc tính. Quan hệ trong cùng phân hệ dùng nét liền. Quan hệ sang phân hệ khác dùng nét đứt và nhãn ngắn như `SupplierId`, `CustomerId`, `StockInId` hoặc `1-N`.

Các đường nối phải:

- Dùng đầu nối vuông góc.
- Đi theo các hành lang trống giữa khung và giữa các bảng.
- Không đi xuyên qua bảng hoặc chữ.
- Không chồng lên nhau; nếu cùng hướng thì tách thành các làn song song.
- Ưu tiên cổng nối gần nhau nhất ở mép bảng.
- Hạn chế giao cắt; nếu không tránh được thì đổi vị trí bảng hoặc đổi hành lang, không dùng đường chéo.

## Thiết kế ERD chi tiết

Mỗi ERD chi tiết giữ đầy đủ thuộc tính, PK và FK của các bảng thuộc phân hệ chính. Bảng thuộc phân hệ khác chỉ xuất hiện dưới dạng ô tham chiếu màu xám có tên bảng, không lặp thuộc tính.

Nguồn quan hệ có thẩm quyền là `QuanLyHangHoa/Data/AppDbContext.cs`. Các model chỉ dùng để đối chiếu kiểu dữ liệu và navigation property.

### Danh mục và đối tác

Bảng chính:

- `Category`, `Brand`, `Unit`, `Product`, `ProductUnit`
- `Supplier`, `Customer`, `Warehouse`

Quan hệ nội bộ:

- `Category 1-N Product`
- `Brand 1-N Product`
- `Unit 1-N Product` qua `DefaultUnitId`
- `Product 1-N ProductUnit`
- `Unit 1-N ProductUnit`

Tham chiếu liên phân hệ bắt buộc:

- `Supplier 1-N StockIn`
- `Supplier 1-N PurchaseInvoice`
- `Customer 1-N StockOut`
- `Customer 1-N SalesInvoice`
- `Customer 1-N WarrantyCoverage`
- `Warehouse` nối tới các chứng từ, số dư, kiểm kê, điều chuyển và số sê-ri có FK kho.

### Nhập/xuất kho và số dư

Bảng chính:

- `StockIn`, `StockInLine`
- `StockOut`, `StockOutLine`
- `StockBalance`, `StockLedger`

Quan hệ nội bộ:

- `StockIn 1-N StockInLine`
- `StockOut 1-N StockOutLine`

Tham chiếu liên phân hệ phải kiểm tra và thể hiện:

- `Supplier`, `Customer`, `Warehouse`, `Product`, `Unit`, `AppUser`
- `ProductSerial` qua dòng nhập, dòng xuất và sổ kho
- `PurchaseInvoice`, `PurchaseInvoiceLine`
- `SalesInvoice`, `SalesInvoiceLine`
- `StockCountLine` và các liên kết điều chỉnh nếu có
- `WarrantyClaim` qua phiếu xuất thay thế

### Điều chuyển, kiểm kê và số sê-ri

Bảng chính:

- `StockTransfer`, `StockTransferLine`
- `StockCountSession`, `StockCountLine`
- `StockAdjustment`, `StockAdjustmentLine`
- `ProductSerial`

Quan hệ nội bộ:

- Quan hệ đầu-dòng của điều chuyển, kiểm kê và điều chỉnh
- `ProductSerial` với dòng điều chuyển hoặc dòng điều chỉnh theo FK thực tế

Tham chiếu liên phân hệ phải kiểm tra và thể hiện:

- `Warehouse`, `Product`, `Unit`, `AppUser`
- `StockIn`, `StockInLine`, `StockOut`, `StockOutLine`
- `StockLedger`
- `WarrantyCoverage`, `WarrantyClaim`

### Hóa đơn

Bảng chính:

- `PurchaseInvoice`, `PurchaseInvoiceLine`
- `SalesInvoice`, `SalesInvoiceLine`

Quan hệ nội bộ:

- Quan hệ đầu-dòng của hóa đơn mua và hóa đơn bán

Tham chiếu liên phân hệ phải thể hiện:

- `Supplier`, `Customer`, `Product`, `Unit`, `AppUser`
- `StockIn`, `StockInLine`
- `StockOut`, `StockOutLine`
- `WarrantyCoverage`

### Bảo hành

Bảng chính:

- `WarrantyCoverage`, `WarrantyClaim`

Quan hệ nội bộ:

- `WarrantyCoverage 1-N WarrantyClaim`
- Khóa ngoại kép của `WarrantyClaim` gồm `WarrantyCoverageId` và `ProductSerialId`

Tham chiếu liên phân hệ phải thể hiện:

- `ProductSerial`, `Customer`, `SalesInvoice`
- `StockOut` qua `ReplacementStockOutId`
- `AppUser` qua người duyệt và người xử lý

### Người dùng và nhật ký

Bảng chính:

- `AppUser`, `AuditLog`, `AuditArchiveManifest`, `WareProClientSession`

Quan hệ nội bộ:

- Quan hệ người tạo tài khoản
- `AppUser 1-N AuditLog` qua `PerformedBy`
- `AppUser 1-N AuditArchiveManifest` qua `ActorId`

Tham chiếu liên phân hệ:

- Các quan hệ `CreatedBy`, `ApprovedBy`, `PostedBy`, `ProcessedBy`
- Trên ERD chi tiết, gom bảng ngoài phân hệ thành các ô tham chiếu theo nhóm nghiệp vụ để tránh hàng chục đường trùng nhau nhưng vẫn ghi rõ tên FK.

## Hình MVVM trong DOCX

Sử dụng `C:\Users\player\Desktop\DATN\final\MVVM.png`. Hình có độ phân giải 1448 x 1086 và mô tả đúng luồng khái niệm:

- `View` liên kết dữ liệu và gửi command tới `ViewModel`.
- `ViewModel` gọi nghiệp vụ hoặc dịch vụ.
- Dữ liệu trả về từ lớp model/nghiệp vụ.

Chèn hình ngay sau đoạn văn trong mục `3.1.1 Trách nhiệm của các tầng`, trước mục `3.2`.

Thêm một câu giải thích rằng khối `Model` trong hình là khái niệm bao quát; trong WarePro, lớp dịch vụ, Entity Framework Core và SQL Server vẫn được tách riêng như Hình 3.1.

Đặt chú thích:

`Hình 3.2 – Luồng tương tác giữa View, ViewModel và Model trong MVVM`

Sau khi chèn:

- Đánh lại toàn bộ số hình Chương 3 phía sau.
- Không thay số bảng, nhưng cập nhật lại số trang trong Danh mục bảng.
- Cập nhật Mục lục, Danh mục hình và Danh mục bảng bằng trường Word.
- Kiểm tra số trang của mọi mục bị dịch chuyển.

## Tiêu chí nghiệm thu

- File Draw.io mở được và có đúng bảy trang.
- Mỗi trang Draw.io xuất được thành một PNG tương ứng.
- Không có đường nối xuyên qua bảng, chữ hoặc chồng lên đường khác.
- ERD tổng quan có sáu khung phân hệ và các kết nối liên phân hệ rõ ràng.
- Các quan hệ trong sáu ERD chi tiết khớp `AppDbContext`.
- DOCX mở được, hình MVVM rõ nét, có chú thích đúng style Caption.
- Số hình Chương 3 liên tục; Danh mục hình và Danh mục bảng có số trang đúng.
- Bản DOCX sau sửa được render và kiểm tra toàn bộ trang, đặc biệt mục 3.1.1 và các trang danh mục.
