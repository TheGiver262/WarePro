# C# bằng code từ số 0

File này dành cho người mới hoàn toàn. Mục tiêu là đọc được code C# trong dự án trước, rồi mới viết code đẹp sau.

## 1. Biến là cái hộp lưu dữ liệu

```csharp
string productCode = "SP001";
string productName = "Laptop Dell";
int quantity = 10;
decimal price = 15000000m;
bool isActive = true;
```

Giải thích:

- `string`: chuỗi chữ.
- `int`: số nguyên.
- `decimal`: số tiền hoặc số thập phân chính xác.
- `bool`: đúng/sai.
- Dấu `m` sau `15000000m` báo cho C# biết đây là decimal.

Trong dự án thật bạn sẽ gặp các property tương tự:

```csharp
public string ProductCode { get; set; } = "";
public decimal DefaultPrice { get; set; }
public bool IsActive { get; set; }
```

## 2. Điều kiện if

`if` dùng để rẽ nhánh.

```csharp
int quantity = 0;

if (quantity <= 0)
{
    Console.WriteLine("So luong phai lon hon 0");
}
else
{
    Console.WriteLine("So luong hop le");
}
```

Trong dự án thật, service không chỉ in lỗi mà ném exception:

```csharp
if (command.Quantity <= 0)
{
    throw new InventoryDomainException("Stock-in quantity must be greater than zero.");
}
```

Ý nghĩa: nếu số lượng sai, dừng nghiệp vụ ngay.

## 3. Hàm

Hàm là một khối code có tên, có thể gọi lại nhiều lần.

```csharp
decimal CalculateTotal(decimal quantity, decimal unitPrice)
{
    return quantity * unitPrice;
}

decimal total = CalculateTotal(2, 15000000m);
```

Giải thích:

- `decimal` trước tên hàm là kiểu dữ liệu trả về.
- `quantity`, `unitPrice` là tham số đầu vào.
- `return` trả kết quả.

Công thức hóa đơn trong dự án cũng là những hàm/tính toán kiểu này:

```csharp
decimal subTotal = quantity * unitPrice;
decimal taxAmount = subTotal * taxRate;
decimal grandTotal = subTotal + taxAmount;
```

## 4. Class và object

Class là bản thiết kế. Object là đối tượng thật tạo từ class.

```csharp
public class Product
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public decimal DefaultPrice { get; set; }
    public bool IsActive { get; set; } = true;
}
```

Tạo object:

```csharp
var product = new Product
{
    ProductCode = "SP001",
    DisplayName = "Laptop Dell",
    DefaultPrice = 15000000m,
    IsActive = true
};
```

Trong dự án, thư mục `QuanLyHangHoa/Models` chứa rất nhiều class kiểu này: `Product`, `StockIn`, `StockOut`, `WarrantyClaim`.

## 5. Property là gì?

Property là dữ liệu công khai của object.

```csharp
public string DisplayName { get; set; } = "";
```

Đọc:

```csharp
Console.WriteLine(product.DisplayName);
```

Ghi:

```csharp
product.DisplayName = "Ten moi";
```

WPF binding đọc property để hiển thị lên UI.

## 6. Constructor

Constructor là hàm đặc biệt chạy khi tạo object.

```csharp
public class ProductService
{
    private readonly string _connectionString;

    public ProductService(string connectionString)
    {
        _connectionString = connectionString;
    }
}
```

Tạo object:

```csharp
var service = new ProductService("Server=...");
```

Trong dự án thật:

```csharp
public AuthenticationService(Func<AppDbContext> contextFactory)
{
    _contextFactory = contextFactory;
}
```

`AuthenticationService` cần `contextFactory` để tạo database context khi đăng nhập.

## 7. public, private, readonly

```csharp
public class AuthenticationService
{
    private readonly Func<AppDbContext> _contextFactory;
}
```

- `public`: bên ngoài class dùng được.
- `private`: chỉ class hiện tại dùng được.
- `readonly`: chỉ gán một lần, thường trong constructor.
- `_contextFactory`: dấu `_` là quy ước tên field private.

Tư duy senior: cái gì không cần lộ ra ngoài thì để `private`.

## 8. List

`List<T>` là danh sách nhiều phần tử cùng kiểu.

```csharp
var products = new List<Product>();

products.Add(new Product { ProductCode = "SP001", DisplayName = "Laptop" });
products.Add(new Product { ProductCode = "SP002", DisplayName = "Mouse" });
```

Duyệt list:

```csharp
foreach (var product in products)
{
    Console.WriteLine(product.DisplayName);
}
```

Trong ViewModel, danh sách hiển thị lên UI thường dùng `ObservableCollection<T>`.

## 9. LINQ từng bước

```csharp
var products = new List<Product>
{
    new Product { ProductCode = "SP001", DisplayName = "Laptop", DefaultPrice = 15000000m, IsActive = true },
    new Product { ProductCode = "SP002", DisplayName = "Mouse", DefaultPrice = 200000m, IsActive = true },
    new Product { ProductCode = "SP003", DisplayName = "Old Keyboard", DefaultPrice = 100000m, IsActive = false }
};
```

Lọc sản phẩm active:

```csharp
var activeProducts = products
    .Where(p => p.IsActive)
    .ToList();
```

Tìm theo mã:

```csharp
var product = products.FirstOrDefault(p => p.ProductCode == "SP001");
```

Sắp xếp theo tên:

```csharp
var sorted = products
    .OrderBy(p => p.DisplayName)
    .ToList();
```

Chỉ lấy tên:

```csharp
var names = products
    .Select(p => p.DisplayName)
    .ToList();
```

Kiểm tra có mã trùng không:

```csharp
bool exists = products.Any(p => p.ProductCode == "SP001");
```

Trong EF Core, LINQ giống vậy nhưng chạy xuống database.

## 10. Lambda là gì?

Trong:

```csharp
p => p.IsActive
```

`p` là từng product trong list. Câu này nghĩa là: với mỗi product `p`, lấy điều kiện `p.IsActive`.

Ví dụ dài hơn:

```csharp
p => p.DefaultPrice > 1000000m && p.IsActive
```

Nghĩa là sản phẩm giá trên 1 triệu và đang active.

## 11. Null

`null` nghĩa là chưa có object.

```csharp
Product? product = null;
```

Dấu `?` cho phép biến null.

Kiểm tra trước khi dùng:

```csharp
if (product != null)
{
    Console.WriteLine(product.DisplayName);
}
```

Trong dự án:

```csharp
private UserControl? _currentView;
```

Vì lúc mới mở app có thể chưa có view hiện tại.

## 12. Exception và try/catch

Ném lỗi:

```csharp
throw new InvalidOperationException("Ma san pham da ton tai.");
```

Bắt lỗi:

```csharp
try
{
    service.CreateProduct(product);
}
catch (InvalidOperationException ex)
{
    MessageBox.Show(ex.Message);
}
```

Trong service, hãy ném lỗi có nghĩa. Trong ViewModel/UI, hãy bắt lỗi và báo cho người dùng.

## 13. Service là gì?

Service là class xử lý nghiệp vụ.

```csharp
public class ProductService
{
    private readonly List<Product> _products = new();

    public void Create(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductCode))
        {
            throw new InvalidOperationException("Ma san pham la bat buoc.");
        }

        if (_products.Any(p => p.ProductCode == product.ProductCode))
        {
            throw new InvalidOperationException("Ma san pham da ton tai.");
        }

        _products.Add(product);
    }

    public List<Product> GetAll()
    {
        return _products
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayName)
            .ToList();
    }
}
```

Tư duy:

- ViewModel gọi service.
- Service kiểm tra nghiệp vụ.
- Service lưu dữ liệu.

## 14. Service với database trong dự án

Trong dự án thật, service không lưu vào list mà lưu vào database:

```csharp
using var db = _contextFactory();
db.Products.Add(product);
db.SaveChanges();
```

Nghĩa là:

1. Tạo database context.
2. Thêm product vào bảng Products.
3. Gọi `SaveChanges` để ghi thật vào SQL Server.

## 15. Attribute của MVVM Toolkit

Code bạn viết:

```csharp
[ObservableProperty]
private string _searchText = "";
```

Toolkit tự sinh tương đương:

```csharp
public string SearchText
{
    get => _searchText;
    set
    {
        if (_searchText != value)
        {
            _searchText = value;
            OnPropertyChanged();
        }
    }
}
```

Code bạn viết:

```csharp
[RelayCommand]
private void Search()
{
}
```

Toolkit tự sinh command tên `SearchCommand`.

Trong XAML bạn binding:

```xml
<Button Command="{Binding SearchCommand}" />
```

## 16. Cách đọc một method

Khi đọc một method, hỏi 6 câu:

1. Hàm tên gì?
2. Đầu vào là gì?
3. Đầu ra là gì?
4. Nó lấy dữ liệu ở đâu?
5. Nó chặn lỗi gì?
6. Nó thay đổi dữ liệu gì?

Ví dụ `Authenticate` trong dự án:

- Tên: `Authenticate`.
- Đầu vào: `username`, `password`.
- Đầu ra: `LoginResult`.
- Dữ liệu: `db.AppUsers`.
- Chặn lỗi: user không tồn tại, inactive, bị khóa, sai password.
- Thay đổi: `LastLoginAt`, `FailedLoginCount`, `LockoutUntil`.

## 17. Bài tập code

Tạo class:

```csharp
public class Warehouse
{
    public int Id { get; set; }
    public string WarehouseCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
```

Tạo service dùng list:

```csharp
public class WarehouseService
{
    private readonly List<Warehouse> _warehouses = new();

    public void Create(Warehouse warehouse)
    {
        // Viet validate o day
    }

    public List<Warehouse> Search(string keyword)
    {
        // Viet LINQ o day
        return new List<Warehouse>();
    }
}
```

Yêu cầu:

- Không cho mã rỗng.
- Không cho mã trùng.
- Search theo mã hoặc tên.
- Chỉ trả kho `IsActive = true`.

Khi làm được bài này, bạn đã có nền C# đủ để chuyển sang WPF/MVVM.
