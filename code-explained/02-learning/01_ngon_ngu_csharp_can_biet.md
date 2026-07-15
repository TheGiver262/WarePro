# C# cần biết để đọc và viết lại dự án

C# là ngôn ngữ chính của dự án. Bạn không cần học toàn bộ C# ngay từ đầu; cần học đúng phần app đang dùng.

## 1. Class và object

Class là bản thiết kế. Object là đối tượng thật được tạo từ class.

Ví dụ trong dự án:

- `Product`: mô tả sản phẩm.
- `StockIn`: phiếu nhập kho.
- `AuthenticationService`: lớp xử lý đăng nhập.
- `MainViewModel`: lớp giữ trạng thái màn hình chính.

Mẫu cần nhớ:

```csharp
public class Product
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
}
```

`Product` là kiểu dữ liệu. Mỗi dòng sản phẩm trong database sẽ trở thành một object `Product`.

## 2. Namespace

Namespace là cách nhóm code để tránh trùng tên.

Ví dụ:

```csharp
namespace QuanLyHangHoa.Services
{
    public class AuthenticationService
    {
    }
}
```

Khi file khác muốn dùng, nó khai báo:

```csharp
using QuanLyHangHoa.Services;
```

Trong dự án, namespace thường khớp với thư mục:

- `QuanLyHangHoa.Models`
- `QuanLyHangHoa.ViewModels`
- `QuanLyHangHoa.Services`
- `QuanLyHangHoa.Inventory`
- `QuanLyHangHoa.Data`

## 3. Property

Property là dữ liệu của object. WPF binding chủ yếu đọc/ghi property.

```csharp
public string Username { get; set; } = "";
```

Trong ViewModel, project dùng `CommunityToolkit.Mvvm` để sinh property tự động:

```csharp
[ObservableProperty]
private string _username = "";
```

Toolkit sẽ sinh ra property public tên `Username` và tự gọi `PropertyChanged` khi giá trị đổi. Đây là cơ chế làm UI tự cập nhật.

## 4. Nullable

Project bật:

```xml
<Nullable>enable</Nullable>
```

Nghĩa là C# sẽ phân biệt biến có thể null và không thể null.

```csharp
private UserControl? _currentView;
```

Dấu `?` nghĩa là `CurrentView` có thể chưa có giá trị. Khi thấy `?`, hãy tự hỏi: trường hợp null có được xử lý chưa?

## 5. Constructor và dependency

Constructor là hàm chạy khi tạo object.

```csharp
public MainViewModel(AppUser user, Func<AppDbContext> contextFactory)
{
    CurrentUser = user;
    ContextFactory = contextFactory;
    OpenDashboard();
}
```

Ý nghĩa:

- `MainViewModel` cần user đang đăng nhập.
- Nó cần một factory tạo `AppDbContext`.
- Khi khởi tạo xong, nó mở dashboard.

Đây là kiểu thiết kế phổ biến: object nhận dependency từ bên ngoài thay vì tự tạo lung tung.

## 6. Interface

Interface là hợp đồng. Nó nói một lớp phải có những hàm nào, không nói chi tiết làm ra sao.

Trong dự án, lớp inventory dùng các interface như `IInventoryUnitOfWork`, `IClock`, `IDefaultWarehouseProvider`.

Lợi ích:

- Dễ test vì có thể thay database thật bằng fake object.
- Dễ đổi cách lưu dữ liệu mà không phá logic nghiệp vụ.
- Giúp `InventoryPostingService` tập trung vào quy tắc kho.

## 7. Collection

Các kiểu collection gặp thường xuyên:

- `List<T>`: danh sách có thứ tự.
- `ObservableCollection<T>`: danh sách dùng cho UI, tự báo khi thêm/xóa item.
- `Dictionary<TKey, TValue>`: tra cứu theo khóa.
- Array: danh sách cố định, ví dụ `string[]`.

Ví dụ trong `MainViewModel`:

```csharp
private readonly Dictionary<string, UserControl> _viewCache = new();
```

Nó lưu màn hình đã mở theo tên, tránh tạo lại view khi người dùng chuyển menu.

## 8. LINQ

LINQ là cách viết truy vấn dữ liệu trong C#.

Ví dụ:

```csharp
var user = db.AppUsers.FirstOrDefault(u => u.Username == username);
```

Ý nghĩa: tìm user đầu tiên có `Username` bằng biến `username`. Nếu không có thì trả về null.

Ví dụ trong xử lý serial:

```csharp
var serialNumbers = command.SerialNumbers
    .Select(s => s.Trim())
    .Where(s => s.Length > 0)
    .ToArray();
```

Luồng xử lý:

1. Lấy từng serial.
2. Xóa khoảng trắng đầu/cuối.
3. Bỏ serial rỗng.
4. Chuyển thành array.

Các hàm LINQ cần học trước:

- `Where`: lọc.
- `Select`: biến đổi.
- `FirstOrDefault`: lấy một phần tử hoặc null.
- `Any`: kiểm tra có phần tử không.
- `Count`: đếm.
- `OrderBy`: sắp xếp.
- `Include`: load quan hệ EF Core.
- `ToList` / `ToArray`: thực thi truy vấn.

## 9. Exception

Exception là lỗi có kiểm soát.

Ví dụ:

```csharp
if (command.Quantity <= 0)
{
    throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
}
```

Đây là cách service chặn dữ liệu sai. Khi tự code nghiệp vụ, đừng để dữ liệu sai đi tiếp xuống database.

## 10. using var

Trong service, bạn sẽ thấy:

```csharp
using var db = _contextFactory();
```

`using var` đảm bảo `db` được giải phóng sau khi hàm kết thúc. Với `DbContext`, đây là cách tránh giữ connection quá lâu.

## 11. Attribute

Attribute là metadata gắn lên class, method hoặc field.

Trong dự án:

```csharp
[ObservableProperty]
private AppUser _currentUser;

[RelayCommand]
private void OpenDashboard()
{
}
```

`[ObservableProperty]` sinh property và notification.  
`[RelayCommand]` sinh command cho WPF binding.

## 12. Record và with

Trong inventory có kiểu record snapshot và cách copy:

```csharp
_unitOfWork.SaveBalance(balance with
{
    OnHandQuantity = balance.OnHandQuantity + (int)command.Quantity
});
```

`with` tạo bản sao từ object cũ, chỉ thay field được chỉ định. Cách này giúp logic rõ hơn: dữ liệu cũ không bị sửa âm thầm.

## 13. decimal, int, DateTime

Trong app quản lý kho, kiểu số rất quan trọng:

- `int`: khóa chính, số lượng nguyên.
- `decimal`: tiền, giá, số lượng có phần lẻ.
- `DateTime`: ngày lập phiếu, ngày ghi sổ, ngày bảo hành.
- `bool`: cờ đúng/sai như `IsActive`, `IsSerialTracked`.

Không dùng `double` cho tiền vì có sai số số học.

## 14. async/await

Một số app WPF dùng `async/await` để không làm đơ UI. Nếu gặp:

```csharp
private async Task LoadDataAsync()
{
    await service.LoadAsync();
}
```

Hãy hiểu đơn giản: hàm có thể chờ việc lâu như đọc database, nhưng UI vẫn phản hồi.

## 15. Bài tập C# trực tiếp trên dự án

1. Mở `QuanLyHangHoa/Models/Product.cs`, liệt kê tất cả property và giải thích mỗi property lưu gì.
2. Mở `QuanLyHangHoa/Services/AuthenticationService.cs`, viết lại bằng lời luồng đăng nhập.
3. Mở `QuanLyHangHoa/Inventory/InventoryPostingService.cs`, tìm tất cả chỗ `throw`.
4. Tự viết class `Student` có `Id`, `Code`, `FullName`, `IsActive`.
5. Tự viết hàm lọc danh sách `Student` theo keyword bằng LINQ.

## 16. Mốc đạt yêu cầu

Bạn đã đủ C# để đọc dự án khi có thể:

- Nhìn một class và biết nó đại diện cho dữ liệu hay logic.
- Nhìn constructor và biết class phụ thuộc vào gì.
- Đọc được một câu LINQ đơn giản.
- Hiểu vì sao service dùng exception để chặn nghiệp vụ sai.
- Hiểu `[ObservableProperty]` và `[RelayCommand]` sinh code cho MVVM.
