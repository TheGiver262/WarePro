# WarePro Defense Deck - Blueprint Redesign

## Mục tiêu

Dựng lại bản thuyết trình bảo vệ đồ án WarePro trong 15 phút, 20 slide, bám tuyệt đối nội dung DOCX/PDF mới nhất trong `E:\Minh\DATN\Final`.

## Chuẩn thị giác

- Tham chiếu trực tiếp: `C:\Users\player\Downloads\WarePro_Technical_Blueprint.pdf`.
- Tỷ lệ 16:9 như PDF tham chiếu (1376 x 768).
- Nền giấy kem/trắng xám; lưới kỹ thuật rất mờ; đường kẻ và chú thích xanh than.
- Màu nhấn lấy từ PDF: xanh lam nhạt, vàng bản vẽ và đỏ nhạt; không dùng tím/violet.
- Tiêu đề rõ, khoảng trắng lớn, một ý chính mỗi slide; không dùng card grid dày đặc.
- Sơ đồ giữ nguyên từ đồ án, đặt như bản vẽ kỹ thuật với khung/caption gọn.

## Quy tắc ảnh giao diện

- Mỗi slide giao diện chỉ có đúng một ảnh.
- Không collage, không ghép nhiều màn hình trên cùng slide.
- Ảnh phải đủ lớn để đọc được; ưu tiên chiếm 60-75% diện tích slide.
- Năm slide giao diện riêng: Số sê-ri, Bảo hành, Nhật ký, Dashboard, Nhập kho.

## Cấu trúc 20 slide

1. Trang bìa
2. Vấn đề và mục tiêu
3. Phạm vi
4. Hai luồng nghiệp vụ
5. Năm vai trò
6. Chuỗi dữ liệu WarePro
7. Kiến trúc WPF/MVVM
8. Sáu phân hệ dữ liệu
9. StockBalance, StockLedger, ProductSerial
10. Vòng đời chứng từ
11. Giao dịch ghi sổ
12. Đồng thời và deadlock
13. Truy vết số sê-ri - ảnh giao diện Serial
14. Bảo hành - ảnh giao diện Bảo hành
15. Phân quyền và nhật ký - ảnh giao diện Nhật ký
16. Dashboard - một ảnh
17. Nhập kho - một ảnh
18. Kết quả kiểm thử
19. Giới hạn và hướng tiếp theo
20. Kết luận và cảm ơn

## Ràng buộc nội dung

- Không đưa nội dung không có trong DOCX/PDF Final.
- 904 kiểm thử được trình bày đúng là 886 kiểm thử ứng dụng + 18 kiểm thử SQL Server tại commit `41cc3a7`.
- Các giới hạn phải nêu đúng: dữ liệu mô phỏng, chưa kiểm thử tải và vận hành dài hạn.
- Mỗi slide có speaker notes và khối `[Sources]`.

## Tiêu chí nghiệm thu

- PowerPoint mở được, đủ 20 slide, 16:9.
- Mỗi slide 13-17 có đúng một ảnh giao diện; không slide nào ghép ảnh.
- Không overflow, clipping, placeholder cũ hoặc nội dung từ template cũ.
- 20/20 tiêu đề khớp content lock mới.
- DOCX/PDF nguồn không bị sửa.
