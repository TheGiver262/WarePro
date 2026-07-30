# Thiết kế topology liên phân hệ cho ERD tổng quan WarePro

Ngày: 2026-07-30  
Tệp đích: `C:\Users\player\Desktop\DATN\final\WarePro_ERD_Tong_20260730.drawio`

## Mục tiêu

- Chỉ sửa trang `ERD tổng quan`; giữ nguyên sáu trang ERD chi tiết.
- Giữ nguyên bảng, bố cục phân hệ và quan hệ nội bộ trong phiên bản Draw.io hiện tại.
- Giữ cách gom nhiều quan hệ bảng thành một trục giữa hai phân hệ.
- Thu gọn topology liên phân hệ đúng theo Hình 3.3 trong DOCX mới nhất.
- Không sửa DOCX và không xuất PNG.

## Ánh xạ mô hình cũ sang mô hình mở rộng

Hình 3.3 cũ có bảy cụm. ERD mở rộng hiện tại có sáu phân hệ vì `Danh mục`
và `Đối tác` được gộp chung, còn `Chứng từ kho` và `Tồn và truy vết` được
phân bổ lại giữa hai phân hệ kho và điều chuyển/kiểm kê/số sê-ri.

## Sáu trục liên phân hệ được phép

1. `Danh mục và đối tác` → `Nhập/xuất kho và số dư`.
2. `Danh mục và đối tác` → `Hóa đơn`.
3. `Nhập/xuất kho và số dư` ↔ `Điều chuyển, kiểm kê và số sê-ri`.
4. `Điều chuyển, kiểm kê và số sê-ri` → `Bảo hành`.
5. `Người dùng và nhật ký` → `Hóa đơn`.
6. `Người dùng và nhật ký` → `Bảo hành`.

Mỗi cặp chỉ có một đường trục màu xanh. Các bảng con có FK thật hội tụ vào
đầu trục phía phân hệ của chúng. Metadata trên trục vẫn lưu danh sách quan hệ
PK/FK được đại diện.

## Quan hệ bị ẩn trên trang tổng quan

Các cặp liên phân hệ khác không xuất hiện, kể cả khi có FK thật, vì Hình 3.3
cũ không nối trực tiếp các cụm đó. Quan hệ đầy đủ vẫn nằm trong sáu ERD chi tiết.

## Bảo toàn dữ liệu và nghiệm thu

- Lấy chính tệp Draw.io hiện tại làm đầu vào vì tệp đã được người dùng chỉnh sửa
  sau lần bàn giao trước.
- Tạo bản sao lưu trước khi thay đổi.
- Trang tổng quan có đúng 6 trục liên phân hệ và không còn trục ngoài danh sách.
- Quan hệ nội bộ trong từng phân hệ không đổi.
- Sáu trang chi tiết giữ nguyên từng byte.
- Tệp mở được bằng Draw.io và qua kiểm tra trực quan về đường đè/chồng chéo.
