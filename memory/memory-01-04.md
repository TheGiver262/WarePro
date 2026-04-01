# Nhật ký dự án: Quản lý hàng hoá (WPF C#) - Ngày 01/04/2026

## 1. Tóm tắt các công việc đã hoàn thành:
1. **Thiết lập kiến trúc hệ thống:** Khởi tạo project WPF C# với .NET 8, áp dụng toàn diện mô hình MVVM (sử dụng gói `CommunityToolkit.Mvvm`).
2. **Thiết kế Database (Entity Framework Core):** 
   - Hoàn thành thiết kế các bảng (Model): `Product`, `Employee`, `Invoice`, `ImportReceipt`, `WarrantyTicket`...
   - Cấu hình chuyển đổi Database từ công nghệ SQL Server (mặc định) sang dùng **SQLite** (file `QuanLyHangHoa.db`). Việc này đã khắc phục hoàn toàn lỗi `Unable to locate Local Database`, giúp Giao diện App chạy được trên máy khách hàng 100% không cần cài phần mềm bổ trợ.
   - Thêm tính năng *Seeding* (Mồi dữ liệu): App tự động nhét 5 tài khoản nhân viên (bao gồm tài khoản *admin/admin*) và 5 sản phẩm kho vào lần khởi động đầu tiên.
3. **Các tính năng Logic lớn đã giải quyết thành công:**
   - **Xác thực và Phân quyền:** Đăng nhập, và chống xâm nhập tab Quản lý nhân viên (Admin) bởi Staff. Phát triển cơ chế "Fail-safe" không cho ai xóa tải khoản Admin ID 1.
   - **Bán hàng & Nhập Kho (Transaction):** Ứng dụng Database Transaction tự động xử lý mượt mà việc trừ/tăng Hàng hóa tồn kho khi lưu phiếu/hoá đơn. An toàn 100% kể cả khi rớt mạng đứng máy, chống lệch số liệu kho.
   - **Bảo hành phân loại Hóa đơn:** Thuật toán gộp nhóm thông minh từ thư viện LINQ giúp phân loại sản phẩm có hạn bảo hành khác nhau trên cùng 1 lần khách yêu cầu sửa, từ đó tự chia tách thành nhiều ticket logic.

## 2. Giải quyết sự cố & Debug:
- **Lỗi thiếu môi trường:** Máy không cài SQL Express $\rightarrow$ Chuyển hoàn toàn sang SQLite.
- **Lỗi UI MaterialDesignThemes (`Cannot locate resource... Default.xaml`):** Sau khi thử các cách, tôi đã bắt được lỗi do Version 5.0 nâng cấp đột ngột. Chúng ta đã gỡ bản v5.0 xuống `v4.6.1` chuẩn ổn định tuyệt đối và thay cấu trúc Load `App.xaml` thủ công. Kết quả: App chạy lên vô cùng nhẹ nhàng và mượt mắt.
- **Lỗi Thiếu Method thư viện:** Bổ sung Namespace `Using Microsoft.EntityFrameworkCore` ở các tầng Service.
   
## 3. Lời khuyên Kiến trúc mở rộng (Tư vấn ngoài lề):
- Nếu mô hình công ty phát triển, chúng ta không cần dùng Docker, Data Pipeline nếu chỉ xài desktop app. 
- Thay vào đó, nếu muốn đồng bộ toàn hệ thống nhiều chi nhánh, chỉ cần đưa Database lên **Server Đám Mây (Cloud Postgres / SQL Server)** và sửa chuỗi Connection ở Client App. Cuối cùng đóng gói file WPF (`Publish - Self Contained Single File .exe`) gửi qua bưu điện/internet là nhân viên cài cái kịch.
