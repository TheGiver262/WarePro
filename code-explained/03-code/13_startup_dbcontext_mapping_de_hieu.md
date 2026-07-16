# 13 - Startup, AppDbContext và mapping database dễ hiểu

> **Trạng thái hiện hành:** startup đi qua `FirstRunCredentialCoordinator` và `StartupCoordinator`. `DatabaseInitializer` vẫn dùng `EnsureCreated` cho database mới, nhưng database hiện hữu còn được bảo vệ bằng schema metadata, compatibility gate, `SchemaUpgradeLock`, verified backup và transaction. Vì vậy không được kết luận hệ thống production chỉ dựa vào `EnsureCreated`.

```text
settings/credential -> SQL probe -> compatibility -> lock
-> EnsureCreated (database mới) -> backup (nếu nâng schema có dữ liệu)
-> schema update -> seed -> login
```

Đọc [chương 14](./14_cai_dat_cap_nhat_phat_hanh_de_hieu.md) để hiểu installer, updater và release gate liên quan tới startup.

File này giải thích các phần khó trong:

- `QuanLyHangHoa/App.xaml.cs`
- `QuanLyHangHoa/Data/AppDbContext.cs`
- `QuanLyHangHoa/Migrations/*`
- `QuanLyHangHoa/Models/*`

Nếu chỉ nhớ một câu: `App.xaml.cs` chuẩn bị database khi app mở, còn `AppDbContext` là bản đồ giữa class C# và bảng SQL Server.

## 1. App khởi động từ đâu?

WPF app bắt đầu từ `App.xaml` và class `App` trong `App.xaml.cs`.

Method quan trọng:

```csharp
protected override void OnStartup(StartupEventArgs e)
```

Dễ hiểu: đây là hàm chạy khi ứng dụng vừa mở.

Trong dự án, `OnStartup` làm ba việc lớn:

```text
1. Gọi base.OnStartup(e)
2. Đảm bảo database tồn tại
3. Chạy một số SQL migration thủ công
4. Seed dữ liệu mẫu từ Excel nếu có file seed
```

## 2. `EnsureCreated` là gì?

Trong `App.xaml.cs`:

```csharp
db.Database.EnsureCreated();
```

Dễ hiểu: nếu database chưa có thì tạo database theo model hiện tại.

Lưu ý quan trọng:

- `EnsureCreated` tiện cho demo/học tập.
- Nó không phải quy trình migration production chuẩn.
- Nếu database đã tồn tại, `EnsureCreated` không tự thêm bảng/cột mới.

Vì vậy file này có thêm phần SQL thủ công để bổ sung bảng/cột còn thiếu.

## 3. Vì sao có migration SQL thủ công trong startup?

Trong code có mảng:

```csharp
var migrations = new[]
{
    "IF NOT EXISTS ... CREATE TABLE StockTransfer ...",
    "IF COL_LENGTH('Product', 'Description') IS NULL ALTER TABLE Product ADD Description NVARCHAR(MAX)",
    ...
};
```

Dễ hiểu: khi app chạy trên database cũ, một số bảng/cột mới có thể chưa tồn tại. Các câu SQL này kiểm tra trước khi tạo/thêm.

Ví dụ:

```sql
IF COL_LENGTH('Product', 'Description') IS NULL
ALTER TABLE Product ADD Description NVARCHAR(MAX)
```

Dịch: nếu bảng `Product` chưa có cột `Description` thì thêm cột đó.

## 4. Vì sao mỗi SQL có try/catch riêng?

```csharp
foreach (var sql in migrations)
{
    try
    {
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration Error: {sql} -> {ex.Message}");
    }
}
```

Dễ hiểu: một câu migration lỗi không làm app dừng hoàn toàn. App ghi lỗi ra console rồi chạy tiếp.

Ưu điểm: tiện demo, ít crash khi DB lệch nhẹ.  
+Nhược điểm: nếu là production nghiêm túc, nên dùng EF Core migrations chuẩn và log lỗi rõ hơn.

## 5. Seed dữ liệu từ Excel

Sau migration thủ công, app tìm file:

```text
Database/warepro_database_seed.xlsx
```

Nếu tìm thấy, chạy:

```csharp
var seeder = new Services.DataImport.DatabaseSeeder(db, excelPath);
var log = Task.Run(async () => await seeder.SeedAsync()).GetAwaiter().GetResult();
```

Dễ hiểu: app đọc workbook rồi đồng bộ dữ liệu mẫu như danh mục, sản phẩm, quy đổi đơn vị, chứng từ và serial. Bản ghi đã tồn tại được nhận diện theo mã; seeder chỉ thêm dữ liệu còn thiếu.

Riêng sheet `ProductUnit` không thể dùng trực tiếp số ID trong Excel vì ID thật trong DB có thể khác. Seeder xử lý theo thứ tự:

1. Đồng bộ `Unit` và `Product`, đồng thời dựng `_unitMap` và `_productMap` từ ID nguồn sang ID thật.
2. Đọc từng dòng `ProductUnit` và resolve hai khóa ngoại qua các map này.
3. Kiểm tra `ConversionFactor > 0`.
4. Chỉ thêm cặp `(ProductId, UnitId)` chưa tồn tại; không ghi đè hệ số/cờ của dữ liệu cũ và không tạo đơn vị cơ sở thứ hai cho cùng sản phẩm.

Nhờ vậy, database mới nhận đủ dữ liệu quy đổi trong workbook, còn database đang sử dụng có thể chạy lại seeder mà không sinh dòng trùng hoặc làm mất cấu hình người dùng.

## 6. AppDbContext là gì?

`AppDbContext` là lớp trung tâm của EF Core.

Nó làm 3 việc:

```text
1. Biết kết nối đến SQL Server nào
2. Biết class nào tương ứng bảng nào
3. Biết quan hệ, khóa, index, kiểu dữ liệu của từng bảng
```

File:

```text
QuanLyHangHoa/Data/AppDbContext.cs
```

## 7. Connection string

Trong `OnConfiguring`:

```csharp
optionsBuilder.UseSqlServer("Server=.\\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;");
```

Dịch:

| Phần | Nghĩa |
|---|---|
| `Server=.\SQLEXPRESS` | SQL Server Express trên máy local |
| `Database=ProductManagementDb` | Tên database |
| `Trusted_Connection=True` | Dùng Windows Authentication |
| `TrustServerCertificate=True` | Tin certificate local |

## 8. DbSet là gì?

Trong `AppDbContext`:

```csharp
public virtual DbSet<Product> Products { get; set; }
public virtual DbSet<StockIn> StockIns { get; set; }
public virtual DbSet<StockLedger> StockLedgers { get; set; }
```

Dễ hiểu:

```text
DbSet<Product> Products ~ bảng Product
DbSet<StockIn> StockIns ~ bảng StockIn
DbSet<StockLedger> StockLedgers ~ bảng StockLedger
```

Khi service viết:

```csharp
var products = db.Products.ToList();
```

EF Core sẽ query bảng `Product`.

## 9. OnModelCreating là gì?

`OnModelCreating` là nơi cấu hình chi tiết cách entity map vào database.

Ví dụ:

```csharp
modelBuilder.Entity<Product>(entity =>
{
    entity.ToTable("Product");
    entity.HasIndex(e => e.ProductCode, "UX_Product_ProductCode").IsUnique();
    entity.Property(e => e.DefaultPrice).HasColumnType("decimal(18, 2)");
});
```

Dịch:

- Class `Product` map vào bảng `Product`.
- `ProductCode` không được trùng.
- `DefaultPrice` lưu bằng decimal 18 chữ số, 2 số lẻ.

## 10. Mapping quan hệ đọc thế nào?

Ví dụ sản phẩm thuộc danh mục:

```csharp
entity.HasOne(d => d.Category).WithMany(p => p.Products)
    .HasForeignKey(d => d.CategoryId)
    .OnDelete(DeleteBehavior.ClientSetNull);
```

Dịch từng đoạn:

| Code | Nghĩa |
|---|---|
| `HasOne(d => d.Category)` | Một Product có một Category |
| `WithMany(p => p.Products)` | Một Category có nhiều Product |
| `HasForeignKey(d => d.CategoryId)` | Khóa ngoại nằm ở Product.CategoryId |
| `OnDelete(DeleteBehavior.ClientSetNull)` | Không tự xóa dây chuyền tùy tiện |

## 11. Index và unique index

Ví dụ:

```csharp
entity.HasIndex(e => e.SerialNumber, "UX_ProductSerial_SerialNumber").IsUnique();
```

Dễ hiểu: serial không được trùng trong toàn hệ thống.

Các unique quan trọng:

| Unique | Vì sao cần |
|---|---|
| `ProductCode` | Mã sản phẩm là định danh nghiệp vụ |
| `SerialNumber` | Một serial chỉ đại diện một thiết bị thật |
| `DocumentCode` | Mã phiếu không được trùng |
| `InvoiceCode` | Mã hóa đơn không được trùng |
| `ClaimCode` | Mã hồ sơ bảo hành không được trùng |

## 12. Check constraint

Ví dụ:

```csharp
entity.ToTable("StockIn", t => t.HasCheckConstraint(
    "CK_StockIn_PurposeCode",
    "[PurposeCode] IN ('Purchase', 'OpeningBalance', 'Adjustment', 'WarrantyReceive')"));
```

Dịch: `StockIn.PurposeCode` chỉ được nằm trong danh sách giá trị hợp lệ.

Nếu app vô tình ghi `PurposeCode = "ABC"`, database sẽ chặn.

## 13. Vì sao AppDbContext dài?

Vì dự án có nhiều phân hệ:

```text
Danh mục: Product, Category, Brand, Unit, Warehouse
Kho: StockIn, StockOut, StockTransfer, StockAdjustment, StockBalance, StockLedger
Serial: ProductSerial
Hóa đơn: SalesInvoice, PurchaseInvoice
Bảo hành: WarrantyCoverage, WarrantyClaim
Hệ thống: AppUser, AuditLog
```

Mỗi entity cần map:

- Tên bảng.
- Khóa chính.
- Index.
- Kiểu cột.
- Quan hệ khóa ngoại.
- Constraint.

Vì vậy `AppDbContext.cs` dài là bình thường.

## 14. Cách đọc AppDbContext không bị ngợp

Không đọc từ đầu đến cuối một lượt. Đọc theo entity bạn đang cần.

Ví dụ muốn hiểu nhập kho:

```text
Tìm model StockIn
-> Tìm DbSet<StockIn>
-> Tìm modelBuilder.Entity<StockIn>
-> Đọc StockInLine
-> Đọc StockLedger
-> Đọc StockBalance
-> Đọc ProductSerial
```

Ví dụ muốn hiểu bảo hành:

```text
Tìm WarrantyCoverage
-> Tìm WarrantyClaim
-> Tìm ProductSerial
-> Tìm SalesInvoice nếu cần biết hóa đơn bán liên quan
```

## 15. Vì sao service dùng `Func<AppDbContext>`?

Trong app desktop, không nên giữ một `DbContext` sống suốt vòng đời app.

Dự án truyền factory:

```csharp
private readonly Func<AppDbContext> _contextFactory;
```

Rồi trong method:

```csharp
using var db = _contextFactory();
```

Dễ hiểu: mỗi nghiệp vụ mở một phiên làm việc database ngắn hạn, dùng xong đóng.

Lợi ích:

- Dữ liệu mới hơn.
- Ít giữ bộ nhớ lâu.
- Ít lỗi tracking.
- Dễ test hơn.

## 16. Migration chuẩn và migration thủ công khác nhau thế nào?

| Cách | Dễ hiểu | Trong dự án |
|---|---|---|
| EF Core migrations | File C# mô tả thay đổi schema, chạy bằng `dotnet ef database update` | Có thư mục `Migrations` |
| SQL thủ công trong startup | App tự chạy `ALTER TABLE`/`CREATE TABLE` khi mở | Có trong `App.xaml.cs` |

WarePro dùng chiến lược migration-in-app có version metadata và các guard dành cho môi trường nhiều máy. Các câu SQL phải idempotent, được khóa, backup và chạy trong transaction; release chỉ được lên stable sau khi Gate C trên SQL disposable đạt.

## 17. Những điểm cần nói thẳng khi bảo vệ

- Connection string được tạo từ runtime settings; SQL password nằm trong Windows Credential Manager theo từng user, không ghi vào `settings.json`.
- `EnsureCreated` chỉ tạo database mới; schema hiện hữu dùng metadata, lock, backup và SQL idempotent. Đây là migration-in-app có guard, không còn là lời gọi demo đơn lẻ.
- Seed Excel chỉ chạy theo policy và nằm trong vùng startup có khóa; production vẫn phải kiểm tra dữ liệu seed trong Gate C.
- `AppDbContext` dài vì hệ thống có nhiều bảng và quan hệ.

## 18. Cách học phần này trong 30 phút

1. Mở `App.xaml.cs`, đọc phần điều phối credential và `StartupCoordinator`.
2. Mở `Startup/StartupCoordinator.cs`, ghi lại thứ tự load settings, probe SQL và initialize database.
3. Mở `Services/DatabaseInitializer.cs`, tìm compatibility, lock, backup, schema update và seed.
4. Mở `AppDbContext.cs`, chỉ đọc `DbSet` trước.
5. Chọn một entity dễ: `Product`.
6. Tìm `modelBuilder.Entity<Product>`.
7. Đọc unique index, decimal price, quan hệ Category/Brand/Unit.
8. Chuyển sang entity khó hơn: `ProductSerial` hoặc `StockIn`.

Đừng cố thuộc toàn bộ mapping. Hãy biết cách tìm đúng đoạn khi cần.
