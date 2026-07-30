# Thiết kế vẽ lại ERD tổng quan WarePro

Ngày: 2026-07-30  
Tệp đích: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`

## Mục tiêu

- Chỉ thay trang 1 `ERD tổng quan`; giữ nguyên từng byte của sáu trang chi tiết.
- Chia 31 bảng thành sáu phân hệ, chỉ hiện tên bảng trên trang tổng quan.
- Dùng đường trực tiếp giữa bảng cha và bảng con cho các FK tiêu biểu; không dùng gateway hoặc đường nối giữa hai khung phân hệ.
- Đặt `Product`, `Warehouse`, `ProductSerial`, `StockLedger` gần biên phân hệ để làm trục liên kết.
- Đặt `WareProClientSession` trong vùng hệ thống và ghi rõ bảng độc lập, không có FK.

## Bố cục

Canvas gồm hai hàng, ba cột:

1. Danh mục và đối tác
2. Nhập/xuất kho và số dư
3. Hóa đơn
4. Người dùng và nhật ký
5. Điều chuyển, kiểm kê và số sê-ri
6. Bảo hành

Các bảng nằm trong khung màu của phân hệ. Bảng trung tâm có viền đậm. Quan hệ
nội bộ dùng nét xám; quan hệ liên phân hệ dùng nét xanh. Đường nối vuông góc,
được tách lane trong khoảng trống giữa các phân hệ.

## Quan hệ

- Nguồn chuẩn duy nhất: `QuanLyHangHoa/Data/AppDbContext.cs`.
- `Product` nối đủ 11 bảng chứa `ProductId`.
- `Warehouse`, `Supplier`, `Customer`, `Unit`, `ProductSerial`, chứng từ kho,
  hóa đơn và bảo hành chỉ nối theo FK thật.
- `AppUser` nối tới các bảng đại diện của từng phân hệ và các bảng nhật ký;
  sáu ERD chi tiết tiếp tục thể hiện đầy đủ mọi vai trò người tạo/duyệt/ghi sổ.
- Không vẽ `StockLedger.SourceDocumentId` như FK.
- Không vẽ quan hệ giả `Product`–`Supplier`, `Product`–`Customer`,
  `Product`–`Warehouse`.

## Nghiệm thu

- Tệp có đúng 7 trang và mở được bằng Draw.io.
- Trang 2–7 giữ nguyên từng byte.
- Trang tổng quan có đủ 6 phân hệ và 31 bảng.
- Mọi cạnh đều tồn tại trong quan hệ trích từ `AppDbContext`.
- `Product` có đúng 11 cạnh tới bảng chứa `ProductId`.
- Không có waypoint nằm trong bảng không phải nguồn hoặc đích.
- Không tạo PNG và không sửa DOCX.
