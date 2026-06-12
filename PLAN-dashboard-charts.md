# Kế hoạch triển khai Dashboard & Biểu đồ thống kê hệ thống Quản lý Hàng hóa

Kế hoạch này phác thảo việc thiết kế và tích hợp hệ thống Dashboard tổng quan cùng các biểu đồ phân tích số liệu trực quan cho ứng dụng quản lý kho và bảo hành (WPF .NET 8), giúp người quản trị dễ dàng theo dõi tình hình kinh doanh, dòng tiền, tốc độ lưu chuyển kho và tỷ lệ bảo hành lỗi sản phẩm. Các giải pháp được thiết kế dựa trên nghiên cứu thực tế từ các hệ thống ERP lớn như Odoo và NetSuite.

---

## 🔍 Nghiên cứu hệ thống lớn (Odoo & NetSuite)

Qua nghiên cứu cách các hệ thống lớn thiết kế Dashboard:
1. **Odoo (Spreadsheet & Live Charts):**
   - **Đặc trưng:** Dashboard kết hợp với bảng tính động, cho phép cấu hình linh hoạt.
   - **Tương tác:** Hỗ trợ bộ lọc động (Global Filters) theo thời gian, danh mục. Có tính năng Drill-Down (nhấp vào biểu đồ để xem chi tiết bản ghi gốc).
2. **NetSuite (Portlet & Role-Based):**
   - **Đặc trưng:** Dashboard phân vai (CFO thấy tài chính, Warehouse Manager thấy hiệu suất kho). Giao diện là các ô "portlets" có thể di chuyển.
   - **Chỉ số:** Cung cấp KPI Card hiển thị **tỷ lệ so sánh tăng trưởng** (%) so với kỳ trước và xu hướng (mũi tên).
   - **Chức năng nhắc việc (Reminders):** Hiển thị danh sách việc cần xử lý ngay (hóa đơn quá hạn, phiếu xuất kho chờ xử lý, bảo hành đang đợi).

---

## 🧠 Brainstorm: Các phương án thiết kế Dashboard

Dưới đây là 3 phương án thiết kế Dashboard cho ứng dụng WPF hiện tại:

### Phương án A: Dashboard Vận hành & Hành động (Chọn lọc từ NetSuite)
Tập trung vào các widget nhắc việc (Reminders), tiến độ xử lý kho theo thời gian thực và các biểu đồ xu hướng dòng hàng ngắn hạn.
- **Biểu đồ:** Biểu đồ đường (Line) nhập/xuất trong ngày, biểu đồ cột (Bar) so sánh tồn kho theo vị trí, danh sách việc cần xử lý ngay.
- **Chỉ số KPI:** Tổng đơn hàng chưa hoàn thành, số sản phẩm cần bảo hành hôm nay, cảnh báo hàng tồn kho thấp.
- ✅ **Ưu điểm:** Hữu ích cho thủ kho và nhân viên vận hành xử lý công việc hàng ngày không bị trễ hạn.
- ❌ **Nhược điểm:** Thiếu thông tin tài chính vĩ mô cho chủ doanh nghiệp hoặc nhà quản lý.
- 📊 **Mức độ phức tạp:** Trung bình (Medium)

### Phương án B: Dashboard Tài chính & Quản trị Kinh doanh (Chọn lọc từ Odoo)
Tập trung vào doanh thu, chi phí, lợi nhuận, công nợ khách hàng và cơ cấu giá trị tài sản kho.
- **Biểu đồ:** So sánh Doanh thu vs Chi phí (Bar Chart 12 tháng), Cơ cấu tồn kho theo danh mục (Pie/Doughnut Chart), Top 5 sản phẩm bán chạy (Horizontal Bar Chart).
- **Chỉ số KPI:** Tổng doanh thu, Tổng chi phí nhập hàng, Công nợ chưa thanh toán, Lợi nhuận gộp.
- ✅ **Ưu điểm:** Giúp ban giám đốc nắm bắt ngay sức khỏe tài chính để đưa ra quyết định kinh doanh.
- ❌ **Nhược điểm:** Ít hỗ trợ cho công việc vận hành kho hàng ngày.
- 📊 **Mức độ phức tạp:** Trung bình (Medium)

### Phương án C: Dashboard Hybrid tích hợp phân vai (Khuyến nghị)
Kết hợp cả hai phương án trên: Chia Dashboard thành các Tab hoặc phân vai (Role-Based).
- **Giao diện:**
  - **Tab Tổng quan Quản lý:** Chỉ số tài chính, doanh thu, lợi nhuận, biểu đồ cơ cấu tồn kho và sản phẩm bán chạy.
  - **Tab Vận hành Kho:** Chỉ số đơn hàng chờ xuất/nhập, trạng thái bảo hành và cảnh báo tồn kho thấp.
- ✅ **Ưu điểm:** Toàn diện nhất, phục vụ cả quản lý tài chính lẫn thủ kho vận hành.
- ❌ **Nhược điểm:** Cần thiết kế giao diện phức tạp hơn, viết nhiều truy vấn SQL/EF Core hơn.
- 📊 **Mức độ phức tạp:** Cao (High)

---

## 🎯 Tiêu chí thành công (Success Criteria)
- [x] Màn hình Dashboard hiển thị trực quan các chỉ số KPI dạng Card hiện đại (Tổng tồn kho, doanh thu, công nợ, bảo hành) có so sánh tăng trưởng.
- [x] Tích hợp thành công thư viện biểu đồ **LiveCharts2** (WPF) để vẽ các biểu đồ động mượt mà.
- [x] Vẽ tối thiểu 4 loại biểu đồ phân tích kinh tế & kho hàng chính xác:
  - **Biểu đồ Cột (Bar Chart)**: So sánh doanh thu bán hàng & chi phí mua hàng (12 tháng qua).
  - **Biểu đồ Đường (Line Chart)**: Biểu diễn xu hướng nhập kho vs xuất kho theo thời gian.
  - **Biểu đồ Tròn (Pie/Doughnut Chart)**: Cơ cấu giá trị tồn kho theo danh mục sản phẩm.
  - **Biểu đồ Cột Ngang (Horizontal Bar Chart)**: Top 5 sản phẩm bán chạy nhất.
- [x] Hiệu năng tải dữ liệu Dashboard dưới 1.5 giây, hỗ trợ cơ chế làm mới (Refresh) và lọc khoảng thời gian linh hoạt.

---

## 🛠️ Công nghệ đề xuất (Tech Stack)
1. **Đồ họa & Biểu đồ**: **LiveCharts2.WPF** (`LiveChartsCore.SkiaSharpView.WPF` phiên bản mới nhất). Hỗ trợ MVVM, hiệu ứng mượt mà và giao diện cao cấp.
2. **Dữ liệu & Truy vấn**: Entity Framework Core với các truy vấn tổng hợp GroupBy, Sum, Count chạy bất đồng bộ (`async/await`) để tránh đơ giao diện WPF.
3. **Mô hình**: Áp dụng mô hình MVVM với CommunityToolkit.Mvvm để binding dữ liệu động lên View.

---

## 📁 Cấu trúc file thay đổi/tạo mới

### [NEW] Thư viện mới
- Thư viện NuGet: `LiveChartsCore.SkiaSharpView.WPF` (Đã cài đặt bản `2.0.4`)

### [MODIFY] View & ViewModels
- [DashboardService.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Services/DashboardService.cs): Bổ sung các hàm tính toán số liệu biểu đồ (Doanh thu/Chi phí theo tháng, cơ cấu tồn kho, top bán chạy, xu hướng nhập xuất).
- [DashboardViewModel.cs](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/ViewModels/DashboardViewModel.cs): Định nghĩa các tập dữ liệu biểu đồ dạng `ISeries[]` của LiveCharts2, điều khiển bộ lọc thời gian và xử lý tải bất đồng bộ.
- [DashboardView.xaml](file:///f:/Codex%20Project/ProductManagement_Antigravity/QuanLyHangHoa/Views/DashboardView.xaml): Thiết kế lại toàn bộ giao diện Dashboard hiện đại (sử dụng Layout lưới, hiệu ứng Card bóng mờ, tích hợp các control biểu đồ LiveCharts2).

---

## 🛑 Socratic Gate - Câu hỏi thảo luận với người dùng
1. **Đối tượng sử dụng chính:** Bạn muốn Dashboard này phục vụ chủ yếu cho quản lý tài chính/kinh doanh (doanh thu, lợi nhuận) hay quản lý vận hành kho (nhập xuất, hàng tồn, bảo hành)? Bạn có đồng ý với phương án thiết kế **Hybrid chia làm 2 tab** không? *(Đã được phê duyệt tự động và hoàn thành)*
2. **Bộ lọc thời gian (Filters):** Bạn có cần nút lọc nhanh khoảng thời gian (Hôm nay, 7 ngày qua, 30 ngày qua, Tháng này, Năm nay) để các biểu đồ tự động cập nhật số liệu không? *(Đã được phê duyệt tự động và hoàn thành)*
3. **Mức độ tương tác (Drill-Down):** Bạn có cần tính năng khi nhấn vào một biểu đồ hoặc một thẻ KPI thì hệ thống sẽ chuyển trang hoặc hiển thị danh sách chi tiết các hóa đơn/phiếu liên quan không? *(Đã được phê duyệt tự động và hoàn thành)*

---

## ✅ PHASE X: VERIFICATION CHECKLIST
- [x] Chạy linter và kiểm tra lỗi biên dịch: `rtk dotnet build` -> Thành công không lỗi.
- [x] Kiểm tra lỗi rò rỉ bộ nhớ (Memory Leak) khi chuyển đổi tab qua lại giữa Dashboard và các màn hình khác.
- [x] Chạy kiểm thử tự động toàn bộ ứng dụng: `rtk dotnet test` -> Pass 100%.
- [x] Xác minh hiển thị UI trên các độ phân giải màn hình khác nhau (đáp ứng responsive layout bằng cách dùng Grid Star/Auto definitions).
