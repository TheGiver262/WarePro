# Workbook tự code app mini quản lý kho

Workbook này biến roadmap thành các bước code nhỏ. Mỗi bước có mục tiêu rõ, file cần tạo, code gợi ý, và tiêu chí tự chấm.

## Quy tắc làm workbook

1. Mỗi bước chỉ làm một việc.
2. Sau mỗi bước phải build.
3. Có lỗi thì sửa ngay, không để dồn.
4. Chưa cần UI đẹp, cần đúng kiến trúc trước.
5. Khi service chạy đúng, sau đó mới nối UI.

## Bước 1: Tạo model Product

Tạo file:

```text
Models/Product.cs
```

Code:

```csharp
namespace WarehouseMini.Models;

public class Product
{
    public int Id { get; set; }
    public string ProductCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public decimal DefaultPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSerialTracked { get; set; }
}
```

Tự chấm:

- Build không lỗi.
- Bạn giải thích được từng property.

## Bước 2: Tạo AppDbContext

Tạo file:

```text
Data/AppDbContext.cs
```

Code:

```csharp
using Microsoft.EntityFrameworkCore;
using WarehouseMini.Models;

namespace WarehouseMini.Data;

public class AppDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(
            "Server=.\\SQLEXPRESS;Database=WarehouseMiniDb;Trusted_Connection=True;TrustServerCertificate=True;");
    }

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
}
```

Tự chấm:

- Bạn biết `DbSet<Product>` là bảng sản phẩm.
- Bạn biết vì sao `ProductCode` unique.

## Bước 3: Tạo ProductService

Tạo file:

```text
Services/ProductService.cs
```

Code:

```csharp
using Microsoft.EntityFrameworkCore;
using WarehouseMini.Data;
using WarehouseMini.Models;

namespace WarehouseMini.Services;

public class ProductService
{
    private readonly Func<AppDbContext> _contextFactory;

    public ProductService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public List<Product> GetAll(string keyword = "")
    {
        using var db = _contextFactory();

        var query = db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(p =>
                p.ProductCode.Contains(keyword) ||
                p.DisplayName.Contains(keyword));
        }

        return query
            .OrderBy(p => p.DisplayName)
            .ToList();
    }

    public void Create(Product product)
    {
        if (string.IsNullOrWhiteSpace(product.ProductCode))
        {
            throw new InvalidOperationException("Ma san pham la bat buoc.");
        }

        if (string.IsNullOrWhiteSpace(product.DisplayName))
        {
            throw new InvalidOperationException("Ten san pham la bat buoc.");
        }

        if (product.DefaultPrice < 0)
        {
            throw new InvalidOperationException("Gia khong duoc am.");
        }

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

Tự chấm:

- Service validate trước khi ghi DB.
- Service không biết gì về WPF.
- ViewModel sau này chỉ gọi service.

## Bước 4: Tạo ProductViewModel

Tạo file:

```text
ViewModels/ProductViewModel.cs
```

Code:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WarehouseMini.Models;
using WarehouseMini.Services;

namespace WarehouseMini.ViewModels;

public partial class ProductViewModel : ObservableObject
{
    private readonly ProductService _service;

    public ObservableCollection<Product> Products { get; } = new();

    [ObservableProperty]
    private string _keyword = "";

    [ObservableProperty]
    private string _productCode = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private decimal _defaultPrice;

    [ObservableProperty]
    private bool _isSerialTracked;

    public ProductViewModel(ProductService service)
    {
        _service = service;
        LoadData();
    }

    [RelayCommand]
    private void LoadData()
    {
        Products.Clear();
        foreach (var product in _service.GetAll(Keyword))
        {
            Products.Add(product);
        }
    }

    [RelayCommand]
    private void Create()
    {
        try
        {
            _service.Create(new Product
            {
                ProductCode = ProductCode,
                DisplayName = DisplayName,
                DefaultPrice = DefaultPrice,
                IsSerialTracked = IsSerialTracked
            });

            ProductCode = "";
            DisplayName = "";
            DefaultPrice = 0;
            IsSerialTracked = false;
            LoadData();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}
```

Tự chấm:

- Có `ObservableCollection`.
- Có `[ObservableProperty]`.
- Có `[RelayCommand]`.
- Lỗi từ service được báo ra UI.

## Bước 5: Tạo ProductView

Tạo file:

```text
Views/ProductView.xaml
```

Code XAML rút gọn:

```xml
<UserControl x:Class="WarehouseMini.Views.ProductView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Margin="16">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <StackPanel Orientation="Horizontal">
            <TextBox Width="180"
                     Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}" />
            <Button Content="Tim"
                    Command="{Binding LoadDataCommand}" />
        </StackPanel>

        <StackPanel Grid.Row="1"
                    Orientation="Horizontal"
                    Margin="0,12,0,12">
            <TextBox Width="120"
                     Text="{Binding ProductCode, UpdateSourceTrigger=PropertyChanged}" />
            <TextBox Width="220"
                     Text="{Binding DisplayName, UpdateSourceTrigger=PropertyChanged}" />
            <TextBox Width="120"
                     Text="{Binding DefaultPrice, UpdateSourceTrigger=PropertyChanged}" />
            <CheckBox Content="Co serial"
                      IsChecked="{Binding IsSerialTracked}" />
            <Button Content="Them"
                    Command="{Binding CreateCommand}" />
        </StackPanel>

        <DataGrid Grid.Row="2"
                  ItemsSource="{Binding Products}"
                  AutoGenerateColumns="False">
            <DataGrid.Columns>
                <DataGridTextColumn Header="Ma" Binding="{Binding ProductCode}" />
                <DataGridTextColumn Header="Ten" Binding="{Binding DisplayName}" />
                <DataGridTextColumn Header="Gia" Binding="{Binding DefaultPrice}" />
                <DataGridCheckBoxColumn Header="Serial" Binding="{Binding IsSerialTracked}" />
            </DataGrid.Columns>
        </DataGrid>
    </Grid>
</UserControl>
```

Tự chấm:

- Gõ form và bấm thêm tạo sản phẩm.
- Danh sách reload sau khi thêm.
- Không có nghiệp vụ trong `.xaml.cs`.

## Bước 6: Tạo Warehouse và StockBalance

Models:

```csharp
public class Warehouse
{
    public int Id { get; set; }
    public string WarehouseCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class StockBalance
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int OnHandQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}
```

Thêm vào DbContext:

```csharp
public DbSet<Warehouse> Warehouses => Set<Warehouse>();
public DbSet<StockBalance> StockBalances => Set<StockBalance>();
```

Mapping:

```csharp
modelBuilder.Entity<Warehouse>(entity =>
{
    entity.ToTable("Warehouse");
    entity.HasKey(w => w.Id);
    entity.HasIndex(w => w.WarehouseCode).IsUnique();
});

modelBuilder.Entity<StockBalance>(entity =>
{
    entity.ToTable("StockBalance");
    entity.HasKey(b => b.Id);
    entity.HasIndex(b => new { b.ProductId, b.WarehouseId }).IsUnique();
});
```

Tự chấm:

- Hiểu một sản phẩm ở một kho chỉ có một dòng balance.

## Bước 7: Tạo StockLedger

Model:

```csharp
public class StockLedger
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public string Direction { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime PostedAt { get; set; }
    public int PostedByUserId { get; set; }
}
```

DbSet:

```csharp
public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
```

Tự chấm:

- Bạn giải thích được balance là số dư, ledger là lịch sử.

## Bước 8: Viết InventoryPostingService nhập kho

```csharp
public class InventoryPostingService
{
    private readonly Func<AppDbContext> _contextFactory;

    public InventoryPostingService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public void PostStockIn(int productId, int warehouseId, int quantity, int userId)
    {
        if (quantity <= 0)
        {
            throw new InvalidOperationException("So luong nhap phai lon hon 0.");
        }

        using var db = _contextFactory();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            var balance = db.StockBalances.FirstOrDefault(b =>
                b.ProductId == productId &&
                b.WarehouseId == warehouseId);

            if (balance == null)
            {
                balance = new StockBalance
                {
                    ProductId = productId,
                    WarehouseId = warehouseId
                };
                db.StockBalances.Add(balance);
            }

            balance.OnHandQuantity += quantity;
            balance.AvailableQuantity += quantity;

            db.StockLedgers.Add(new StockLedger
            {
                ProductId = productId,
                WarehouseId = warehouseId,
                Direction = "IN",
                Quantity = quantity,
                PostedAt = DateTime.Now,
                PostedByUserId = userId
            });

            db.SaveChanges();
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

Tự chấm:

- Quantity <= 0 bị chặn.
- Balance tăng.
- Ledger có dòng IN.
- Có transaction.

## Bước 9: Viết xuất kho

```csharp
public void PostStockOut(int productId, int warehouseId, int quantity, int userId)
{
    if (quantity <= 0)
    {
        throw new InvalidOperationException("So luong xuat phai lon hon 0.");
    }

    using var db = _contextFactory();
    using var transaction = db.Database.BeginTransaction();

    try
    {
        var balance = db.StockBalances.FirstOrDefault(b =>
            b.ProductId == productId &&
            b.WarehouseId == warehouseId);

        if (balance == null || balance.AvailableQuantity < quantity)
        {
            throw new InvalidOperationException("Khong du ton kho.");
        }

        balance.OnHandQuantity -= quantity;
        balance.AvailableQuantity -= quantity;

        db.StockLedgers.Add(new StockLedger
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            Direction = "OUT",
            Quantity = quantity,
            PostedAt = DateTime.Now,
            PostedByUserId = userId
        });

        db.SaveChanges();
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

Tự chấm:

- Xuất quá tồn bị chặn.
- Balance giảm đúng.
- Ledger có dòng OUT.

## Bước 10: Viết test tư duy

Test quan trọng nhất:

```csharp
[Fact]
public void PostStockIn_WhenQuantityIsPositive_IncreasesBalance()
{
    // Arrange
    var service = new InventoryPostingService(CreateContext);

    // Act
    service.PostStockIn(productId: 1, warehouseId: 1, quantity: 10, userId: 1);

    // Assert
    using var db = CreateContext();
    var balance = db.StockBalances.Single(b => b.ProductId == 1 && b.WarehouseId == 1);
    Assert.Equal(10, balance.OnHandQuantity);
    Assert.Equal(10, balance.AvailableQuantity);
}
```

Và:

```csharp
[Fact]
public void PostStockOut_WhenInsufficientStock_Throws()
{
    var service = new InventoryPostingService(CreateContext);

    Assert.Throws<InvalidOperationException>(() =>
        service.PostStockOut(productId: 1, warehouseId: 1, quantity: 999, userId: 1));
}
```

Tự chấm:

- Test tên mô tả rõ điều kiện và kết quả.
- Test không phụ thuộc UI.

## Bước 11: Nối UI sau khi service đúng

Chỉ sau khi `InventoryPostingService` chạy đúng, hãy tạo:

- `StockInViewModel`
- `StockInView`
- `StockOutViewModel`
- `StockOutView`

ViewModel chỉ nên:

- Nhận input từ UI.
- Gọi service.
- Báo lỗi/thành công.
- Reload dữ liệu.

Không nên:

- Tự tính transaction.
- Tự update nhiều bảng.
- Tự validate serial phức tạp trong UI.

## Bước 12: So sánh với dự án thật

Khi app mini chạy được, mở lại dự án thật:

- Mini `ProductService` tương ứng `QuanLyHangHoa/Services/ProductService.cs`.
- Mini `ProductViewModel` tương ứng `QuanLyHangHoa/ViewModels/ProductViewModel.cs`.
- Mini `InventoryPostingService` tương ứng `QuanLyHangHoa/Inventory/InventoryPostingService.cs`.
- Mini `StockBalance` tương ứng `QuanLyHangHoa/Models/StockBalance.cs`.
- Mini `StockLedger` tương ứng `QuanLyHangHoa/Models/StockLedger.cs`.

Lúc này code thật sẽ không còn quá lớn nữa, vì bạn đã tự xây bản nhỏ của cùng một ý tưởng.
