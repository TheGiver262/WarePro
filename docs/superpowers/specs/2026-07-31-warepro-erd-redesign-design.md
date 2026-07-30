# Thiết kế làm lại ERD WarePro

## Mục tiêu

Sửa trực tiếp `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio` để cả bảy trang ERD có chất lượng tương đương hoặc tốt hơn năm ảnh tham chiếu PlantUML trong `F:\DoAnTotNghiep_QuanLyKhoBaoHanh\04_Tai_nguyen\Diagram\plantuml-png-erd-module`.

Kết quả phải dễ đọc khi mở trong draw.io và khi chèn vào báo cáo, đồng thời phản ánh đúng mô hình EF Core hiện tại. Không tạo DOCX hoặc ảnh bàn giao.

## Phạm vi

Thiết kế lại cả bảy trang hiện có:

1. ERD tổng quan
2. Danh mục và đối tác
3. Nhập xuất kho và số dư
4. Điều chuyển kiểm kê và số sê-ri
5. Hóa đơn
6. Bảo hành
7. Người dùng và nhật ký

Giữ nguyên tên trang và định dạng draw.io nhiều trang. Tạo backup trước khi ghi đè file Desktop.

## Nguồn sự thật

- `QuanLyHangHoa/Data/AppDbContext.cs` quyết định FK, optionality, cardinality, khóa kép, unique index và quan hệ tự tham chiếu.
- Các lớp trong `QuanLyHangHoa/Models` cung cấp tên cột và kiểu dữ liệu cần trình bày.
- Graph hiện có chỉ hỗ trợ định vị mã và kiểm tra chéo; không được dùng để suy diễn thêm quan hệ.
- Không tạo FK từ các cột tham chiếu nghiệp vụ không có ràng buộc trong EF Core, ví dụ `SourceDocumentId`.
- Không nối trực tiếp `Product` với `Supplier`, `Customer` hoặc `Warehouse` nếu `AppDbContext` không khai báo FK đó.

## Ngôn ngữ trình bày

- Nền trắng; bảng xám nhạt theo phong cách ảnh PlantUML.
- Viền mảnh mang màu phân hệ, không dùng tím hoặc violet.
- Tên bảng đậm; có biểu tượng thực thể nhỏ ở header nếu không làm giảm khả năng đọc.
- Mỗi bảng chia ba vùng: tên bảng, cột dữ liệu, ràng buộc/index quan trọng.
- Hiển thị kiểu SQL và ký hiệu `[PK]`, `[FK]`, `[UQ]`, `[AK]`, nullable.
- Font Times New Roman; cỡ chữ đủ đọc ở mức zoom vừa và khi chèn vào báo cáo.
- Quan hệ dùng Crow's Foot chuẩn. Nhãn chỉ ghi vai trò ngắn hoặc ràng buộc đặc biệt.
- Bảng thuộc phân hệ hiện tại dùng viền liền; bảng ngoại phân hệ dùng viền nét đứt và dữ liệu rút gọn.
- Ghi chú nghiệp vụ dùng nền vàng nhạt, chỉ xuất hiện khi Crow's Foot không diễn đạt đủ.

## Bố cục từng trang

### 1. ERD tổng quan

- Giữ bố cục sáu khung phân hệ dạng 3 x 2.
- Mỗi phân hệ chứa thẻ bảng thu gọn với tên và PK/FK chính.
- Quan hệ nội bộ nằm hoàn toàn trong khung phân hệ.
- Quan hệ liên phân hệ được gom thành đường trục giữa hai khung rồi phân nhánh tới các bảng liên quan.
- Không để đường chạy vòng quanh toàn trang hoặc xuyên qua khung/bảng.

### 2. Danh mục và đối tác

- Đặt `Product` ở trung tâm.
- Bố trí `Category`, `Brand`, `Unit`, `ProductUnit` quanh `Product`.
- Đặt `Supplier`, `Customer`, `Warehouse` thành các bảng độc lập đúng mô hình thật.
- Chỉ thêm bảng ngoại phân hệ khi cần giải thích một FK thật; đặt sát cạnh liên quan và dùng dạng tham chiếu rút gọn.

### 3. Nhập xuất kho và số dư

- Trình bày luồng trái sang phải: `StockIn` và dòng nhập, tồn/sổ kho, dòng xuất và `StockOut`.
- Đặt `Product`, `Warehouse`, `AppUser` ở hàng tham chiếu phía trên.
- Đặt liên kết với kiểm kê, hóa đơn và bảo hành gần bảng phát sinh FK.
- Giữ `StockBalance` và `StockLedger` ở trung tâm vì đây là hai nguồn dữ liệu tồn kho quan trọng.

### 4. Điều chuyển kiểm kê và số sê-ri

- Chia ba cụm ngang: điều chuyển, kiểm kê, điều chỉnh.
- Đặt `ProductSerial` ở dưới trung tâm làm nút liên kết.
- Đặt `Warehouse`, `Product`, `Unit`, `AppUser` thành hàng tham chiếu phía trên.
- Tách rõ hai FK kho đi/đến của `StockTransfer` và các vai trò người tạo/duyệt/ghi sổ.

### 5. Hóa đơn

- Tạo hai nhánh đối xứng mua và bán.
- Đặt `Product` và `Unit` ở giữa.
- Nhánh mua dùng `Supplier`, `StockIn`, `PurchaseInvoice`, `PurchaseInvoiceLine`.
- Nhánh bán dùng `Customer`, `StockOut`, `SalesInvoice`, `SalesInvoiceLine`.
- Ghi chú ngắn về trạng thái hóa đơn và liên kết một-một tùy chọn với chứng từ kho.

### 6. Bảo hành

- Đặt `WarrantyCoverage` và `WarrantyClaim` ở trung tâm.
- Bố trí `ProductSerial`, `Customer`, `SalesInvoice`, `StockOut`, `AppUser` quanh hai bảng lõi.
- Làm nổi bật FK kép `WarrantyCoverage.(Id, ProductSerialId)` tới `WarrantyClaim.(WarrantyCoverageId, ProductSerialId)`.
- Gắn nhãn ngắn cho serial lỗi, serial thay thế, phiếu xuất thay thế và người xử lý/duyệt.

### 7. Người dùng và nhật ký

- Đặt `AppUser` ở trung tâm.
- Đặt `AuditLog` và `AuditArchiveManifest` bên trái.
- Gom các chứng từ có vai trò người tạo/duyệt/ghi sổ thành nhóm bảng tham chiếu rút gọn bên phải.
- Đặt `WareProClientSession` riêng và ghi rõ bảng độc lập, không có FK trong `AppDbContext`.
- Thể hiện quan hệ tự tham chiếu `AppUser.CreatedBy` bằng một vòng nối ngắn.

## Quy tắc quan hệ và tuyến nối

- Mỗi cạnh đại diện đúng một FK hoặc một FK kép đã khai báo trong EF Core.
- Cardinality và optionality phải khớp kiểu nullable và cấu hình quan hệ.
- Quan hệ nhiều vai trò giữa cùng hai bảng dùng điểm thoát/điểm vào khác nhau.
- Dùng đường vuông góc và hành lang ngoài bảng; không để đường xuyên bảng.
- Hạn chế tối đa đoạn trùng; không đặt nhãn lên bảng hoặc lên nhãn khác.
- Không dùng nhãn dài dạng `Principal.Id -> Dependent.ForeignKey` trên mọi cạnh. Chỉ dùng nhãn vai trò ngắn; tên FK đã hiện trong bảng.
- Các cạnh liên phân hệ trên trang chi tiết đi tới bảng tham chiếu gần nhất, không chạy tới một cột bảng phụ xa ở mép trang.

## Kiểm tra và nghiệm thu

- File có đúng bảy trang, đúng tên và mở được trong draw.io.
- Danh sách bảng, cột trọng yếu và toàn bộ quan hệ được đối chiếu với `AppDbContext` và model hiện tại.
- Không thiếu hoặc thừa FK; không có quan hệ giả.
- FK kép bảo hành và quan hệ tự tham chiếu người dùng được thể hiện rõ.
- Render cả bảy trang để kiểm tra trực quan.
- Không có cạnh xuyên bảng; các đoạn trùng mới được giảm tối đa và được ghi nhận nếu không thể loại bỏ.
- Chữ không tràn bảng, không lỗi mã hóa tiếng Việt, không dùng màu tím/violet.
- Bản gốc được backup; chỉ file draw.io Desktop được ghi đè khi ứng viên đã qua kiểm tra.

## Ngoài phạm vi

- Không sửa DOCX, PDF hoặc ảnh PlantUML nguồn.
- Không tạo PNG/SVG bàn giao; ảnh render chỉ dùng tạm cho QA.
- Không thay đổi mô hình dữ liệu, migration hoặc mã ứng dụng.
