# EF Core và SQL bằng code từ số 0

File này giúp bạn hiểu cách C# lưu dữ liệu vào SQL Server qua Entity Framework Core.

## 1. SQL Server lưu gì?

SQL Server lưu dữ liệu theo bảng.

Ví dụ bảng `Product`:

| Id | ProductCode | DisplayName | DefaultPrice |
|---|---|---|---|
| 1 | SP001 | Laptop | 15000000 |
| 2 | SP002 | Mouse | 200000 |

Trong C#, mỗi dòng có thể là một object:

```csharp
var product = new Product
{
    Id = 1,
    ProductCode = "SP001",
    DisplayName = "Laptop",
    DefaultPrice = 15000000m
};
```

## 2. Entity đầu tiên

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

Quy ước:

- `Id` là khóa chính.
- `ProductCode` là mã nghiệp vụ, không nên trùng.
- `IsActive` dùng để xóa mềm.

## 3. DbContext đầu tiên

```csharp
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            "Server=.\\SQLEXPRESS;Database=WarehouseMiniDb;Trusted_Connection=True;TrustServerCertificate=True;");
    }
}
```

`DbSet<Product>` đại diện bảng `Products`.

Trong dự án thật, file tương ứng là `QuanLyHangHoa/Data/AppDbContext.cs`.

## 4. Mapping bằng OnModelCreating

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Product>(entity =>
    {
        entity.ToTable("Product");
        entity.HasKey(p => p.Id);
        entity.HasIndex(p => p.ProductCode).IsUnique();
        entity.Property(p => p.ProductCode).HasMaxLength(50);
        entity.Property(p => p.DisplayName).HasMaxLength(200);
        entity.Property(p => p.DefaultPrice).HasColumnType("decimal(18,2)");
        entity.Property(p => p.IsActive).HasDefaultValue(true);
    });
}
```

Giải thích:

- `ToTable`: tên bảng thật.
- `HasKey`: khóa chính.
- `HasIndex(...).IsUnique()`: không cho trùng mã.
- `HasMaxLength`: giới hạn độ dài chuỗi.
- `HasColumnType`: kiểu dữ liệu SQL.
- `HasDefaultValue`: giá trị mặc định.

## 5. Thêm dữ liệu

```csharp
using var db = new AppDbContext();

var product = new Product
{
    ProductCode = "SP001",
    DisplayName = "Laptop Dell",
    DefaultPrice = 15000000m
};

db.Products.Add(product);
db.SaveChanges();
```

Điểm quan trọng:

- `Add` mới đánh dấu là sẽ thêm.
- `SaveChanges` mới ghi thật vào database.

## 6. Đọc dữ liệu

Lấy tất cả:

```csharp
using var db = new AppDbContext();
var products = db.Products.ToList();
```

Lấy sản phẩm active:

```csharp
var products = db.Products
    .Where(p => p.IsActive)
    .OrderBy(p => p.DisplayName)
    .ToList();
```

Tìm theo mã:

```csharp
var product = db.Products
    .FirstOrDefault(p => p.ProductCode == "SP001");
```

## 7. Sửa dữ liệu

```csharp
using var db = new AppDbContext();

var product = db.Products.FirstOrDefault(p => p.ProductCode == "SP001");

if (product != null)
{
    product.DisplayName = "Laptop Dell New";
    product.DefaultPrice = 14500000m;
    db.SaveChanges();
}
```

EF Core theo dõi object lấy ra từ database. Khi bạn đổi property và gọi `SaveChanges`, EF tạo câu SQL update.

## 8. Xóa mềm

Không nên xóa thật sản phẩm đã phát sinh giao dịch. Dùng xóa mềm:

```csharp
using var db = new AppDbContext();

var product = db.Products.FirstOrDefault(p => p.ProductCode == "SP001");

if (product != null)
{
    product.IsActive = false;
    db.SaveChanges();
}
```

Khi query danh sách, luôn lọc:

```csharp
var activeProducts = db.Products
    .Where(p => p.IsActive)
    .ToList();
```

## 9. Service dùng DbContext factory

Dự án thật không `new AppDbContext()` trực tiếp khắp nơi. Nó truyền factory:

```csharp
public class ProductService
{
    private readonly Func<AppDbContext> _contextFactory;

    public ProductService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public List<Product> GetAll()
    {
        using var db = _contextFactory();
        return db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.DisplayName)
            .ToList();
    }

    public void Create(Product product)
    {
        using var db = _contextFactory();

        bool exists = db.Products.Any(p => p.ProductCode == product.ProductCode);
        if (exists)
        {
            throw new InvalidOperationException("Ma san pham da ton tai.");
        }

        db.Products.Add(product);
        db.SaveChanges();
    }
}
```

Lợi ích:

- Mỗi nghiệp vụ có DbContext riêng.
- Dễ test.
- Tránh context sống quá lâu trong app desktop.

## 10. Relationship một-nhiều

Một `Category` có nhiều `Product`.

Entity:

```csharp
public class Category
{
    public int Id { get; set; }
    public string CategoryCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}
```

Mapping:

```csharp
modelBuilder.Entity<Product>(entity =>
{
    entity.HasOne(p => p.Category)
        .WithMany(c => c.Products)
        .HasForeignKey(p => p.CategoryId);
});
```

Load kèm category:

```csharp
var products = db.Products
    .Include(p => p.Category)
    .ToList();
```

## 11. StockBalance tối giản

```csharp
public class StockBalance
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int OnHandQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}
```

Một sản phẩm trong một kho chỉ nên có một dòng balance.

Mapping:

```csharp
modelBuilder.Entity<StockBalance>(entity =>
{
    entity.HasIndex(b => new { b.ProductId, b.WarehouseId }).IsUnique();
});
```

## 12. Transaction tối giản

Ví dụ nhập kho phải vừa tăng balance vừa ghi ledger:

```csharp
using var db = _contextFactory();
using var transaction = db.Database.BeginTransaction();

try
{
    var balance = db.StockBalances
        .FirstOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);

    if (balance == null)
    {
        balance = new StockBalance
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            OnHandQuantity = 0,
            AvailableQuantity = 0
        };
        db.StockBalances.Add(balance);
    }

    balance.OnHandQuantity += quantity;
    balance.AvailableQuantity += quantity;

    db.StockLedgers.Add(new StockLedger
    {
        ProductId = productId,
        WarehouseId = warehouseId,
        Quantity = quantity,
        Direction = "IN",
        PostedAt = DateTime.Now
    });

    db.SaveChanges();
    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

Nếu ghi ledger lỗi, phần tăng balance cũng rollback.

## 13. AsNoTracking

Khi chỉ đọc để hiển thị, không cần EF theo dõi thay đổi:

```csharp
var products = db.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToList();
```

Lợi ích: nhanh hơn và ít tốn bộ nhớ hơn.

## 14. Migration là gì?

Khi bạn viết entity/mapping, database thật chưa tự đổi. Migration là file mô tả thay đổi schema.

```powershell
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Nếu mới học, hãy hiểu trước:

- Entity là thiết kế trong C#.
- Migration là cầu nối từ thiết kế C# sang database thật.
- Database update áp dụng migration vào SQL Server.

## 15. Bài tập

Tự viết mini database có:

- `Product`
- `Warehouse`
- `StockBalance`
- `StockLedger`

Yêu cầu:

1. Thêm sản phẩm.
2. Thêm kho.
3. Nhập kho 10 cái.
4. Kiểm tra balance tăng lên 10.
5. Kiểm tra ledger có một dòng `IN`.

Pseudo-code:

```csharp
var productId = CreateProduct("SP001", "Laptop");
var warehouseId = CreateWarehouse("KHO01", "Kho chinh");

PostStockIn(productId, warehouseId, 10);

var balance = GetBalance(productId, warehouseId);
Assert.Equal(10, balance.OnHandQuantity);

var ledger = GetLedgers(productId, warehouseId).Single();
Assert.Equal("IN", ledger.Direction);
Assert.Equal(10, ledger.Quantity);
```

Làm được bài này là bạn đã nắm nền EF Core đủ để hiểu phần kho của dự án.
