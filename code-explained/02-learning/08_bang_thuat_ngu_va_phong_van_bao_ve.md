# Bảng thuật ngữ và câu hỏi bảo vệ

Tài liệu này giúp bạn nói được ngôn ngữ của dự án khi học, phỏng vấn hoặc bảo vệ đồ án.

## Thuật ngữ C#/.NET

| Thuật ngữ | Nghĩa ngắn | Trong dự án |
|---|---|---|
| Class | Bản thiết kế object | `Product`, `StockInService` |
| Object | Đối tượng cụ thể khi chạy | Một sản phẩm đang hiển thị trên UI |
| Property | Dữ liệu của object | `ProductCode`, `DisplayName` |
| Constructor | Hàm khởi tạo object | `MainViewModel(AppUser user, Func<AppDbContext> contextFactory)` |
| Namespace | Không gian tên nhóm class | `QuanLyHangHoa.Services` |
| Interface | Hợp đồng hành vi | `IInventoryUnitOfWork` |
| Exception | Lỗi có kiểm soát | `InventoryDomainException` |
| LINQ | Truy vấn dữ liệu bằng C# | `.Where(...).ToList()` |
| Attribute | Metadata gắn vào code | `[ObservableProperty]`, `[RelayCommand]` |
| Nullable | Biến có thể null | `UserControl?` |

## Thuật ngữ WPF/MVVM

| Thuật ngữ | Nghĩa ngắn | Trong dự án |
|---|---|---|
| WPF | Framework UI desktop Windows | Toàn bộ app `QuanLyHangHoa` |
| XAML | Markup mô tả UI | `Views/*.xaml` |
| View | Màn hình giao diện | `StockInView.xaml` |
| ViewModel | State và command của View | `StockInViewModel.cs` |
| Model | Dữ liệu/entity | `Product.cs`, `StockIn.cs` |
| Binding | Nối UI với property/command | `{Binding SaveCommand}` |
| DataContext | Object làm nguồn binding | `new MainViewModel(...)` |
| Command | Hành động UI gọi được | `OpenStockInViewCommand` |
| Converter | Biến đổi dữ liệu cho UI | `NullToVisibilityConverter` |

## Thuật ngữ EF Core/database

| Thuật ngữ | Nghĩa ngắn | Trong dự án |
|---|---|---|
| DbContext | Phiên làm việc với DB | `AppDbContext` |
| DbSet | Đại diện bảng | `DbSet<Product>` |
| Entity | Class map với bảng | `Product`, `StockLedger` |
| Migration | Lịch sử đổi schema | `QuanLyHangHoa/Migrations` |
| Primary key | Khóa chính | `Id` |
| Foreign key | Khóa ngoại | `Product.CategoryId` |
| Index | Tối ưu tìm kiếm | `IX_Product_BrandId` |
| Unique constraint | Chống trùng | `UX_ProductSerial_SerialNumber` |
| Transaction | Gói thao tác atomic | Ghi sổ kho |

## Thuật ngữ nghiệp vụ kho

| Thuật ngữ | Nghĩa ngắn |
|---|---|
| StockIn | Phiếu nhập kho |
| StockOut | Phiếu xuất kho |
| StockTransfer | Phiếu chuyển kho |
| StockAdjustment | Phiếu điều chỉnh kho |
| StockCount | Kiểm kê |
| StockBalance | Số dư tồn kho hiện tại |
| StockLedger | Sổ lịch sử biến động kho |
| ProductSerial | Serial/IMEI của sản phẩm |
| OnHandQuantity | Số lượng đang có trên sổ |
| AvailableQuantity | Số lượng có thể xuất |
| Posted | Đã ghi sổ |
| Draft | Bản nháp |
| AuditLog | Nhật ký ai làm gì |

## Câu hỏi bảo vệ kiến trúc

### 1. Dự án dùng kiến trúc gì?

Dự án dùng kiến trúc phân lớp kết hợp MVVM. UI nằm ở View/XAML, trạng thái và command nằm ở ViewModel, nghiệp vụ nằm ở Service, dữ liệu nằm ở Model và AppDbContext, còn thuật toán kho lõi nằm trong lớp Inventory.

### 2. Vì sao dùng MVVM?

MVVM giúp tách giao diện khỏi logic. View chỉ hiển thị, ViewModel giữ state và command, Service xử lý nghiệp vụ. Nhờ vậy code dễ test, dễ sửa UI, và tránh nhét xử lý database vào code-behind.

### 3. Vì sao không dùng một DbContext toàn app?

App desktop chạy lâu. Nếu giữ một DbContext sống quá lâu, nó dễ giữ cache cũ, tracking quá nhiều object và gây lỗi khó đoán. Dự án dùng `Func<AppDbContext>` để mỗi nghiệp vụ tạo context ngắn hạn rồi dispose.

### 4. Vì sao cần transaction khi ghi sổ kho?

Ghi sổ kho ảnh hưởng nhiều bảng cùng lúc: phiếu, dòng phiếu, tồn kho, serial, ledger, audit. Nếu một bước thành công rồi bước sau lỗi, dữ liệu sẽ lệch. Transaction đảm bảo hoặc tất cả thành công, hoặc rollback toàn bộ.

### 5. Vì sao có StockBalance rồi vẫn cần StockLedger?

`StockBalance` trả lời hiện còn bao nhiêu. `StockLedger` trả lời vì sao số tồn thay đổi. Balance phục vụ truy vấn nhanh, ledger phục vụ truy vết, kiểm toán và báo cáo lịch sử.

### 6. Vì sao serial phải unique?

Serial/IMEI định danh một sản phẩm vật lý cụ thể. Nếu trùng serial, hệ thống không thể biết đang bảo hành, bán, chuyển kho hay truy vết thiết bị nào.

### 7. Vì sao password dùng BCrypt?

BCrypt là thuật toán hash password có salt và chi phí tính toán, phù hợp để lưu mật khẩu an toàn hơn plain text hoặc hash yếu. Khi login, hệ thống verify password người dùng nhập với hash đã lưu.

### 8. Khi xuất kho, hệ thống chặn lỗi gì?

Hệ thống chặn số lượng <= 0, chặn xuất quá tồn khả dụng, chặn serial sai sản phẩm, serial ở kho khác, serial không còn trạng thái `InStock`, và chặn trùng serial trong cùng phiếu.

### 9. Khi nhập kho sản phẩm có serial, quy tắc là gì?

Nếu sản phẩm có quản lý serial, số lượng serial nhập phải bằng số lượng nhập kho. Serial không được rỗng, không được trùng trong phiếu và không được tồn tại trước đó trong database.

### 10. AuditLog khác StockLedger thế nào?

AuditLog ghi hành động hệ thống: ai làm gì, lúc nào. StockLedger ghi biến động kho: sản phẩm nào, kho nào, tăng/giảm bao nhiêu, từ chứng từ nào. Hai bảng phục vụ hai mục đích khác nhau.

## Câu hỏi tự luyện theo module

Login:

- Nếu user nhập sai mật khẩu 5 lần thì sao?
- Vì sao cần kiểm tra `IsActive`?
- Vì sao cần phân biệt username đúng chữ hoa/thường?

CRUD:

- Vì sao mã sản phẩm phải unique?
- Khi nào xóa mềm thay vì xóa cứng?
- ViewModel và Service chia trách nhiệm thế nào?

Kho:

- Nhập kho ảnh hưởng bảng nào?
- Xuất kho ảnh hưởng bảng nào?
- Chuyển kho cần mấy dòng ledger?
- Điều gì xảy ra nếu xuất quá tồn?

Bảo hành:

- WarrantyCoverage và WarrantyClaim khác nhau thế nào?
- Khi đổi serial mới, hệ thống cần cập nhật những gì?
- Vì sao không nên mở nhiều claim active cho cùng một serial?

Import:

- Vì sao cần validate trước khi ghi database?
- Nếu một dòng Excel sai thì nên xử lý thế nào?
- Vì sao cần báo lỗi theo dòng?

## Cách trả lời tốt

Công thức trả lời:

```text
Khái niệm -> Lý do dùng -> File trong dự án -> Ví dụ luồng thực tế
```

Ví dụ:

```text
Transaction là cơ chế gom nhiều thao tác database thành một đơn vị atomic.
Dự án dùng transaction khi ghi sổ kho vì nhập/xuất kho ảnh hưởng nhiều bảng.
Code nằm trong các service kho và gọi InventoryPostingService.
Ví dụ nhập kho phải tạo phiếu, tăng StockBalance, tạo ProductSerial, ghi StockLedger và AuditLog; nếu một bước lỗi thì rollback toàn bộ.
```

Trả lời như vậy vừa có lý thuyết, vừa chứng minh bạn hiểu code thật.
