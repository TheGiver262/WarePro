# Thiết kế rút gọn tham chiếu ngoài trong ERD phân hệ WarePro

## Mục tiêu

Giảm số lượng bảng và đường nối lặp lại trong sáu ERD phân hệ của
`C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio`,
nhưng vẫn giữ đầy đủ cấu trúc vật lý của các bảng thuộc chính phân hệ đang mô tả.

## Phạm vi

- Chỉ sửa sáu ERD phân hệ; không sửa trang `ERD tổng quan`.
- Không sửa DOCX, PDF hoặc schema database.
- Luôn lấy file Draw.io mới nhất trên Desktop làm nguồn để giữ các chỉnh sửa thủ công gần nhất.
- Tạo backup ngay trước khi thay file Desktop.

## Quy tắc chọn quan hệ

1. Giữ toàn bộ bảng thuộc phân hệ hiện tại và mọi quan hệ FK giữa các bảng đó.
2. Chỉ biểu diễn quan hệ ngoài phân hệ khi FK nằm trên bảng thuộc phân hệ hiện tại.
3. Không biểu diễn quan hệ tham chiếu ngược khi FK nằm trên bảng của phân hệ khác.
4. Các quan hệ tham chiếu ngược được thể hiện tại ERD của phân hệ sở hữu FK và tại ERD tổng quan.
5. Cardinality phải lấy từ nullability, unique index và FK trong schema hiện hành.

Ví dụ: `WarrantyCoverage.SalesInvoiceId` thuộc phân hệ bảo hành. Quan hệ này xuất
hiện trong ERD bảo hành, không lặp lại trong ERD hóa đơn.

## Cách biểu diễn phân hệ ngoài

Mỗi phân hệ ngoài được gom thành một hộp duy nhất:

```text
PHÂN HỆ DANH MỤC VÀ ĐỐI TÁC
Supplier · Customer · Product · Unit
```

- Hộp chỉ chứa tên phân hệ và danh sách tên bảng thực sự được tham chiếu.
- Không hiển thị thuộc tính, PK/FK hoặc constraint trong hộp phân hệ ngoài.
- Một đường trunk vuông góc đi từ hộp phân hệ ngoài đến vùng gần các bảng nội bộ,
  sau đó chia nhánh ngắn đến từng bảng sở hữu FK.
- Không gắn nhãn trên cạnh; tên FK vẫn nằm trong bảng nội bộ.
- Không dùng màu tím hoặc violet.

## Thành phần từng ERD

### Danh mục và đối tác

- Bảng nội bộ: `Category`, `Brand`, `Unit`, `Product`, `ProductUnit`,
  `Supplier`, `Customer`, `Warehouse`.
- Không có hộp phân hệ ngoài.
- Chia cùng trang thành ba cụm:
  - Sản phẩm: `Product`, `Category`, `Brand`, `Unit`, `ProductUnit`.
  - Đối tác: `Supplier`, `Customer`.
  - Kho hàng: `Warehouse`.

### Nhập/xuất kho và số dư

- Bảng nội bộ: `StockIn`, `StockInLine`, `StockOut`, `StockOutLine`,
  `StockBalance`, `StockLedger`.
- Hộp ngoài:
  - Danh mục và đối tác.
  - Người dùng và nhật ký.
  - Điều chuyển, kiểm kê và số sê-ri.

### Điều chuyển, kiểm kê và số sê-ri

- Bảng nội bộ: `StockTransfer`, `StockTransferLine`, `StockCountSession`,
  `StockCountLine`, `StockAdjustment`, `StockAdjustmentLine`, `ProductSerial`.
- Hộp ngoài:
  - Danh mục và đối tác.
  - Người dùng và nhật ký.
  - Nhập/xuất kho và số dư.

### Hóa đơn

- Bảng nội bộ: `PurchaseInvoice`, `PurchaseInvoiceLine`, `SalesInvoice`,
  `SalesInvoiceLine`.
- Hộp ngoài:
  - Danh mục và đối tác.
  - Nhập/xuất kho và số dư.
  - Người dùng và nhật ký.
- Không hiển thị `WarrantyCoverage`; quan hệ do bảng bảo hành sở hữu FK.
- Giữ các chỉnh sửa vật lý đã có: `rowversion`, `decimal(9,4)`, độ dài
  `nvarchar`, `datetime2(0)` và các unique key hợp lệ.

### Bảo hành

- Bảng nội bộ: `WarrantyCoverage`, `WarrantyClaim`.
- Hộp ngoài:
  - Danh mục và đối tác.
  - Hóa đơn.
  - Nhập/xuất kho và số dư.
  - Người dùng và nhật ký.

### Người dùng và nhật ký

- Bảng nội bộ: `AppUser`, `AuditLog`, `AuditArchiveManifest`,
  `WareProClientSession`.
- Không có hộp phân hệ ngoài.
- `WareProClientSession` vẫn độc lập vì schema không có FK nối với bảng khác.

## Bố cục và routing

- Bảng nội bộ nằm ở vùng trung tâm, sắp theo luồng cha → chứng từ → dòng chi tiết.
- Hộp phân hệ ngoài nằm trên hoặc dưới vùng bảng nội bộ, không đặt xen giữa bảng.
- Đường nối dùng routing orthogonal, không xuyên bảng hoặc tiêu đề.
- Các nhánh của cùng một trunk tách ở vùng trắng; không chồng lên trunk khác.
- Kích thước bảng khít với nội dung và dùng phông chữ thống nhất.

## Tiêu chí nghiệm thu

- File vẫn có đủ bảy trang và trang tổng quan byte-identical.
- Sáu ERD phân hệ giữ đủ bảng nội bộ đã liệt kê.
- Không còn thẻ bảng riêng lẻ cho bảng ngoài phân hệ.
- Không còn quan hệ tham chiếu ngược trên ERD không sở hữu FK.
- Quan hệ nội bộ và cardinality khớp schema hiện hành.
- Không có đường xuyên bảng, chồng bảng hoặc đoạn nối không vuông góc.
- Render và kiểm tra trực quan cả sáu ERD phân hệ trước khi thay file Desktop.
