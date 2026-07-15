# Tổng quan Kiến trúc Hệ thống (Architecture Overview)

**WarePro** (project kỹ thuật `QuanLyHangHoa`) được xây dựng trên nền tảng **.NET 8 (Windows Presentation Foundation - WPF)**, áp dụng mô hình kiến trúc phân lớp (Layered Architecture) kết hợp mô hình thiết kế **MVVM (Model-View-ViewModel)** và sử dụng **Entity Framework Core (EF Core)** để truy xuất cơ sở dữ liệu SQL Server.

Tài liệu này giải thích chi tiết cấu trúc phân lớp, nguyên lý giao tiếp giữa các thành phần và các cơ chế xử lý hệ thống cốt lõi.

---

## 1. Cấu trúc Phân lớp Hệ thống (Layered Architecture)

Hệ thống được chia thành 4 lớp chức năng độc lập nhằm đảm bảo nguyên tắc Single Responsibility (Đơn nhiệm) và dễ dàng bảo trì:

```
+-------------------------------------------------------------+
| Presentation Layer (Views, ViewModels - WPF XAML & MVVM)   |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
| Application Layer (Services: Business Logic & Orchestration)|
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
| Domain Layer (Entities: AppUser, Product, ProductSerial...) |
+-------------------------------------------------------------+
                              |
                              v
+-------------------------------------------------------------+
| Infrastructure Layer (EF Core, SQL Server, Transactions)    |
+-------------------------------------------------------------+
```

### A. Presentation Layer (Lớp hiển thị)
* **Thành phần:** Nằm trong thư mục `Views/` (các file XAML/XAML.cs) và `ViewModels/` (các file ViewModel kế thừa từ `ObservableObject`).
* **Vai trò:** Hiển thị giao diện người dùng (UI), bắt các sự kiện tương tác của người dùng, thực hiện Data Binding để đồng bộ hóa dữ liệu hiển thị và thực thi các Command của người dùng.
* **Quy tắc thiết kế:** View không chứa logic nghiệp vụ. Logic điều khiển giao diện được đẩy toàn bộ về ViewModel. ViewModel tương tác với lớp Service phía dưới và không biết gì về UI WPF cụ thể (giúp dễ viết Unit Test).

### B. Application Layer (Lớp dịch vụ ứng dụng)
* **Thành phần:** Thư mục `Services/`.
* **Vai trò:** Chứa các lớp nghiệp vụ (Business Services) như `StockInService`, `StockOutService`, `WarrantyClaimService`... Lớp này nhận yêu cầu từ ViewModel, thực hiện kiểm tra các quy tắc nghiệp vụ (Business Rules) và điều phối việc đọc/ghi dữ liệu.
* **Quy tắc thiết kế:** Không tương tác trực tiếp với giao diện. Mỗi Service thường nhận một `Func<AppDbContext>` (Factory) để tạo kết nối database khi cần.

### C. Domain Layer (Lớp nghiệp vụ lõi)
* **Thành phần:** Thư mục `Models/` và `Inventory/` (các Domain Models và logic xử lý cốt lõi về kho).
* **Vai trò:** Định nghĩa các thực thể (Entities) trong cơ sở dữ liệu và các quy tắc nghiệp vụ bất biến (ví dụ: số lượng tồn kho không được âm, số Serial không được trùng).

### D. Infrastructure Layer (Lớp hạ tầng)
* **Thành phần:** Thư mục `Data/` (chứa `AppDbContext.cs`) và các thư viện ngoài.
* **Vai trò:** Cung cấp kết nối tới SQL Server, quản lý Database Transaction, thực thi các truy vấn SQL thông qua LINQ to Entities và ghi nhật ký kiểm toán hệ thống (`AuditLog`).

---

## 2. Mô hình WPF / MVVM & Data Binding

Hệ thống sử dụng thư viện **CommunityToolkit.Mvvm** để triển khai mô hình MVVM một cách tối giản và hiệu quả.

### A. Cơ chế Data Binding
ViewModel định nghĩa các thuộc tính dạng Reactive bằng cách sử dụng Source Generator thông qua attribute `[ObservableProperty]`:
```csharp
[ObservableProperty]
private string _username = string.Empty;
```
* **Thuật toán biên dịch:** Source Generator sẽ tự động sinh ra thuộc tính public `Username` có đầy đủ mã nguồn gọi sự kiện `OnPropertyChanged` khi giá trị thay đổi. 
* **Liên kết UI:** Trong file XAML của View, UI liên kết trực tiếp với thuộc tính này qua cú pháp `{Binding Username}`. Bất kỳ thay đổi nào từ UI (người dùng nhập) hoặc ViewModel (gán giá trị mới) đều tự động cập nhật lẫn nhau.

### B. Lệnh tương tác (Commands)
Thay vì xử lý sự kiện click nút bấm trong file code-behind (`xaml.cs`), hệ thống định nghĩa các Command trong ViewModel bằng attribute `[RelayCommand]`:
```csharp
[RelayCommand]
private void Login() { ... }
```
* **Cơ chế:** Source Generator tự động sinh ra đối tượng `LoginCommand` kiểu `IRelayCommand`. Nút bấm trong UI liên kết qua `{Binding LoginCommand}`. Điều này giúp tách rời hoàn toàn giao diện khỏi mã xử lý.

---

## 3. Cơ chế Quản lý Lifetime của DbContext (`Func<AppDbContext>`)

Trong các ứng dụng Desktop (WPF/WinForms), kết nối database thường có vòng đời dài. Nếu dùng chung một `DbContext` duy nhất trong suốt thời gian ứng dụng chạy sẽ dẫn đến rò rỉ bộ nhớ (Memory Leak), lỗi cache thực thể cũ và xung đột đa luồng.
Để giải quyết triệt để, hệ thống áp dụng mẫu thiết kế **DbContext Factory**:

```csharp
private readonly Func<AppDbContext> _contextFactory;

public StockInService(Func<AppDbContext> contextFactory)
{
    _contextFactory = contextFactory;
}
```
* **Nguyên lý hoạt động:** Mỗi khi một nghiệp vụ cần truy xuất DB, nó sẽ gọi `using var db = _contextFactory();`. 
* **Vòng đời ngắn (Short-lived DbContext):** Khởi tạo DbContext $\rightarrow$ Thực thi truy vấn $\rightarrow$ Lưu thay đổi $\rightarrow$ Tự động Dispose (đóng kết nối) khi ra khỏi khối `using`. Điều này đảm bảo kết nối DB luôn mới, dữ liệu chính xác và giải phóng tài nguyên CPU/RAM tức thì.

---

## 4. Quản lý Giao dịch (Database Transactions)

Để đảm bảo tính nhất quán của dữ liệu (ACID), đặc biệt là khi thực hiện các nghiệp vụ ghi sổ kho (phát sinh nhiều bảng cùng lúc như Phiếu nhập, Dòng chi tiết, Số Serial, Tồn kho, Sổ kho và Nhật ký), hệ thống luôn sử dụng **Explicit Transaction**:

```csharp
using var db = _contextFactory();
using var transaction = db.Database.BeginTransaction();
try
{
    // 1. Thực hiện các câu lệnh INSERT, UPDATE, DELETE...
    db.SaveChanges();
    
    // 2. Gọi logic xử lý kho (Post)
    postingService.PostStockIn(...);

    // 3. Commit nếu tất cả thành công
    transaction.Commit();
}
catch (Exception)
{
    transaction.Rollback(); // Khôi phục trạng thái ban đầu nếu bất kỳ bước nào lỗi
    throw;
}
```
* **Nguyên lý:** Nếu có bất kỳ lỗi nào xảy ra trong quá trình ghi sổ (ví dụ: mất mạng, trùng serial, DB constraint failed), toàn bộ các bảng dữ liệu sẽ được khôi phục về nguyên trạng trước khi giao dịch bắt đầu. Dữ liệu không bao giờ bị rơi vào trạng thái mâu thuẫn (ví dụ: tạo phiếu nhập nhưng không có tồn kho tương ứng).
