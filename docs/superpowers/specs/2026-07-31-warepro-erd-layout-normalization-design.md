# Thiết kế chuẩn hóa layout ERD WarePro

## Mục tiêu

Sắp xếp lại layout trong `C:\Users\player\Desktop\DATN\Final\WarePro_ERD_Tong_20260730.drawio` theo phong cách trang **Hóa đơn** hiện tại: bảng ôm sát nội dung, đường nối vuông góc và tách bạch, hạn chế tối đa giao cắt hoặc đi xuyên bảng.

## Phạm vi

- Sửa 6 trang: Tổng quan, Danh mục và đối tác, Nhập/xuất kho và số dư, Điều chuyển/kiểm kê/số sê-ri, Bảo hành, Người dùng và nhật ký.
- Không sửa trang Hóa đơn. Khối XML `<diagram>` của trang này phải giữ nguyên từng byte so với snapshot nguồn mới nhất.
- Giữ nguyên tên trang, danh sách bảng, nội dung thuộc tính, cặp quan hệ và lực lượng quan hệ hiện có.
- Không sửa DOCX, PDF hoặc các file Draw.io tham khảo khác.
- Tạo bản sao lưu từ file Desktop mới nhất ngay trước khi ghi đè.

## Bố cục

### ERD phân hệ

- Bảng chính của phân hệ nằm ở vùng trung tâm và tạo thành luồng nghiệp vụ dễ đọc.
- Bảng tham chiếu ngoài phân hệ nằm quanh mép, tương tự bố cục trang Hóa đơn.
- Ưu tiên đặt bảng cha gần bảng con có nhiều quan hệ nhất.
- Không giữ khoảng trống lớn chỉ để cân đối hình; canvas và khoảng cách giữa các cụm phải bám theo nội dung thực tế.

### ERD tổng quan

- Giữ nguyên sáu khung phân hệ và các bảng nằm trong từng phân hệ.
- Co khung phân hệ theo vùng bao thực tế của các bảng bên trong.
- Quan hệ nội bộ đi trong khung; quan hệ liên phân hệ thoát qua mép gần nhất và đi trên các hành lang giữa khung.
- Không thay đổi cấu trúc quan hệ đã được duyệt.

## Kích thước bảng

- Chiều cao tính theo số dòng hiển thị, chiều cao dòng và padding trên/dưới tối thiểu.
- Chiều rộng tính theo dòng text dài nhất, cỡ chữ hiện có và padding trái/phải tối thiểu.
- Không cắt text, không xuống dòng ngoài ý muốn và không để phần thân bảng trống quá mức.
- Bảng tham chiếu ngắn phải nhỏ gọn như các bảng `Customer`, `StockOut`, `Product` trên trang Hóa đơn.

## Routing quan hệ

- Mọi đoạn nối phải nằm ngang hoặc dọc; không có đoạn chéo.
- Mỗi cặp bảng tiếp tục chỉ có một đường nối.
- Phân bố cổng kết nối theo các cạnh bảng để các đường rời nhau ngay từ đầu.
- Dùng lane riêng cho các đường song song; không để hai đường trùng toàn bộ hoặc dùng chung một đoạn dài khó phân biệt.
- Không cho đường đi xuyên bảng, tiêu đề, thuộc tính hoặc khung phân hệ.
- Ưu tiên đường ngắn, ít khúc gấp; chỉ đi vòng ngoài khi tuyến trực tiếp gây va chạm.

## Cách thực hiện

- Tái sử dụng bộ sinh Draw.io và bộ kiểm tra hiện có; chỉ bổ sung logic kích thước và tọa độ cần thiết.
- Đọc geometry của trang Hóa đơn làm chuẩn về mật độ và khoảng cách, nhưng không tái sinh hoặc serialize lại trang này.
- Ghép 6 trang đã chỉnh vào snapshot bằng thay thế raw `<diagram>` theo trang; giữ nguyên raw block trang Hóa đơn.

## Kiểm chứng

- File mở và export được bằng Draw.io.
- Đủ 7 trang, đúng tên và đúng thứ tự.
- Raw SHA-256 của trang Hóa đơn trước/sau giống nhau.
- Không thay đổi tập bảng, tập cặp quan hệ hoặc lực lượng quan hệ.
- Tất cả cạnh đều orthogonal.
- Không có bảng chồng nhau, đường xuyên bảng hoặc bảng cắt text.
- Không có bảng có khoảng trống dọc vượt quá một dòng nội dung cộng padding.
- Render trực quan 6 trang đã sửa và kiểm tra ở mức toàn trang trước khi thay file Desktop.

## Ngoài phạm vi

- Không chỉnh màu sắc hoặc nội dung trang Hóa đơn.
- Không thêm nhãn quan hệ, gateway, bundle hoặc bảng mới.
- Không thay đổi mô hình dữ liệu hay khóa ngoại.
