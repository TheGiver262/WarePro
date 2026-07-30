# Thiết kế giản lược ERD WarePro

## Mục tiêu

Sửa `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio` theo phong cách ERD PlantUML/Mermaid: dễ đọc, ít đường nối và gần như không cần chỉnh tay.

## Phạm vi

- Giữ nguyên 7 trang hiện có: một trang tổng quan và sáu trang phân hệ.
- Giữ tên trang, danh sách bảng và nội dung thuộc tính hiện có.
- Không sửa DOCX, PDF hoặc hai file mẫu trong Downloads.
- Tạo bản sao lưu từ chính file Desktop mới nhất ngay trước khi ghi đè.

## Quy tắc quan hệ

- Chỉ vẽ quan hệ khóa ngoại có thật trong `AppDbContext.cs` và mô hình dữ liệu hiện hành.
- Mỗi cặp bảng chỉ có tối đa một đường nối trên một trang.
- Nếu nhiều khóa ngoại cùng nối một cặp bảng, chúng dùng chung một đường.
- Đường nối không có nhãn; khóa ngoại vẫn được thể hiện trong danh sách thuộc tính của bảng.
- Dùng ký hiệu crow's-foot ở hai đầu để thể hiện lực lượng quan hệ.
- Không dùng gateway, bundle, bus phân hệ hoặc đường nối giả.
- Tham chiếu logic không có khóa ngoại vật lý chỉ được ghi chú, không vẽ như quan hệ FK.

## Bố cục và hình thức

- Bảng cha đặt trước, bảng chứng từ ở giữa, bảng dòng chi tiết và bảng phụ thuộc đặt sau.
- Đường nối vuông góc, ngắn và đi trong khoảng trống; hạn chế tối đa giao cắt và không xuyên qua bảng.
- Bảng trong phân hệ dùng nền trắng, tiêu đề màu nhạt theo phân hệ, viền xám xanh và không đổ bóng.
- Bảng ngoài phân hệ dùng viền nét đứt, chỉ hiển thị các trường cần thiết để hiểu quan hệ.
- Phông chữ rõ ràng, thống nhất; không sử dụng tím hoặc violet.
- Trang tổng quan vẫn chia sáu vùng phân hệ nhưng nối trực tiếp giữa các bảng, không nối qua khối phân hệ.

## Tiêu chí nghiệm thu

- File mở và xuất được bằng Draw.io.
- Đủ 7 trang và đúng tên trang.
- Không thiếu hoặc thừa cặp quan hệ so với nguồn dữ liệu.
- Không có cặp bảng trùng đường nối.
- Không có nhãn trên đường nối.
- Không có gateway hoặc đường gom phân hệ.
- Không có đường xuyên qua bảng; các giao cắt còn lại phải được giảm tới mức hợp lý bằng bố cục phân tầng.
- Render trực quan cả 7 trang để kiểm tra trước khi thay file Desktop.
