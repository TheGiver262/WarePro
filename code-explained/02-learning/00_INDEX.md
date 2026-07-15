# Roadmap học và tự code lại WarePro

Bộ tài liệu này dành cho mục tiêu: hiểu nhanh công nghệ WarePro đang dùng, đọc được code hiện tại trong project `QuanLyHangHoa`, rồi tự code lại một phiên bản từ đầu đến cuối.

Dự án là ứng dụng desktop Windows viết bằng:

- C# trên .NET 8
- WPF và XAML cho giao diện
- MVVM với CommunityToolkit.Mvvm
- Entity Framework Core với SQL Server
- Unit test bằng dự án `QuanLyHangHoa.Tests`
- Import Excel/CSV bằng ClosedXML và CsvHelper

## Cách học nhanh nhất

Đừng học C# như một môn độc lập trong 3 tháng rồi mới mở dự án. Hãy học theo vòng lặp:

1. Học một khái niệm nhỏ.
2. Tìm file thật trong dự án có dùng khái niệm đó.
3. Đọc và giải thích lại bằng lời của mình.
4. Tự viết lại một bản nhỏ hơn.
5. Chạy test hoặc build để kiểm chứng.

## Thứ tự đọc

1. [01_ngon_ngu_csharp_can_biet.md](./01_ngon_ngu_csharp_can_biet.md)  
   Nắm C# đủ để đọc dự án: class, property, nullable, LINQ, exception, attribute, async, collection.

2. [02_wpf_xaml_mvvm_can_biet.md](./02_wpf_xaml_mvvm_can_biet.md)  
   Hiểu WPF, XAML, binding, DataContext, command, ViewModel.

3. [03_ef_core_sql_server_can_biet.md](./03_ef_core_sql_server_can_biet.md)  
   Hiểu cách app nói chuyện với SQL Server qua EF Core.

4. [04_kien_truc_du_an_quan_ly_hang_hoa.md](./04_kien_truc_du_an_quan_ly_hang_hoa.md)  
   Nhìn tổng thể kiến trúc: Views, ViewModels, Services, Models, Inventory, Data.

5. [05_nghiep_vu_chinh_va_thuat_toan.md](./05_nghiep_vu_chinh_va_thuat_toan.md)  
   Hiểu các thuật toán nghiệp vụ: đăng nhập, nhập kho, xuất kho, chuyển kho, kiểm kê, bảo hành.

6. [06_tu_code_lai_du_an_tu_dau.md](./06_tu_code_lai_du_an_tu_dau.md)  
   Kế hoạch tự code lại dự án theo từng module.

7. [07_bai_tap_theo_tuan.md](./07_bai_tap_theo_tuan.md)  
   Lịch học 8 tuần có bài tập, tiêu chí tự chấm.

8. [08_bang_thuat_ngu_va_phong_van_bao_ve.md](./08_bang_thuat_ngu_va_phong_van_bao_ve.md)  
   Bảng thuật ngữ và câu hỏi thường gặp khi giải thích/bảo vệ dự án.

## File nên mở song song khi học

- `QuanLyHangHoa/QuanLyHangHoa.csproj`: biết app dùng framework và thư viện nào.
- `QuanLyHangHoa/App.xaml.cs`: điểm khởi động ứng dụng.
- `QuanLyHangHoa/MainWindow.xaml.cs`: tạo `MainViewModel`.
- `QuanLyHangHoa/ViewModels/MainViewModel.cs`: điều hướng màn hình.
- `QuanLyHangHoa/Data/AppDbContext.cs`: bản đồ database.
- `QuanLyHangHoa/Services/AuthenticationService.cs`: đăng nhập.
- `QuanLyHangHoa/Inventory/InventoryPostingService.cs`: lõi ghi sổ kho.
- `QuanLyHangHoa/Services/StockInService.cs`: luồng nhập kho cấp ứng dụng.
- `QuanLyHangHoa/ViewModels/StockInViewModel.cs`: logic màn hình nhập kho.
- `QuanLyHangHoa/Views/StockInView.xaml`: UI nhập kho.
- `QuanLyHangHoa.Tests/Inventory/PostStockInTests.cs`: test nghiệp vụ nhập kho.

## Nguyên tắc đọc code dự án này

Khi gặp một màn hình, đọc theo thứ tự:

1. `Views/*.xaml`: màn hình có nút, bảng, ô nhập nào?
2. `ViewModels/*.cs`: nút đó gọi command nào, property nào thay đổi?
3. `Services/*.cs`: nghiệp vụ thật được xử lý ở đâu?
4. `Data/AppDbContext.cs`: dữ liệu được map vào bảng nào?
5. `Models/*.cs`: entity gồm những field nào?
6. `Tests/*.cs`: hành vi nào đã được kiểm chứng?

## Đích cuối

Sau roadmap này, bạn cần làm được 5 việc:

1. Giải thích được app chạy từ login đến dashboard.
2. Tự tạo một màn hình CRUD mới theo pattern có sẵn.
3. Tự thêm một bảng/entity mới vào EF Core.
4. Tự viết một nghiệp vụ kho nhỏ có transaction và test.
5. Tự code lại bản mini của app: login, danh mục sản phẩm, nhập kho, xuất kho, tồn kho.

## Nếu bạn chưa biết gì về C#, WPF, EF Core

Đọc thêm 4 chương code chậm dưới đây. Đây là phần "cầm tay chỉ việc", viết cho người mới hoàn toàn:

9. [09_csharp_bang_code_tu_so_0.md](./09_csharp_bang_code_tu_so_0.md)  
   Học C# bằng ví dụ code rất nhỏ: biến, hàm, class, list, LINQ, exception, service.

10. [10_wpf_mvvm_bang_code_tu_so_0.md](./10_wpf_mvvm_bang_code_tu_so_0.md)  
    Tạo màn hình WPF tối giản, hiểu binding, command, ObservableObject, ViewModel.

11. [11_efcore_sql_bang_code_tu_so_0.md](./11_efcore_sql_bang_code_tu_so_0.md)  
    Tự viết DbContext, entity, CRUD, relationship, transaction bằng code nhỏ.

12. [12_workbook_tu_code_app_mini.md](./12_workbook_tu_code_app_mini.md)  
    Workbook thực hành tự code app mini quản lý kho theo từng bước nhỏ.
