# EF Core và SQL Server cần biết

Dự án dùng Entity Framework Core để làm cầu nối giữa C# và SQL Server. Thay vì viết SQL trực tiếp ở mọi nơi, app dùng class C# đại diện cho bảng và LINQ để truy vấn.

## 1. Database trong dự án

Tên database mặc định trong `AppDbContext`:

```text
ProductManagementDb
```

Connection string:

```csharp
Server=.\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;
```

Ý nghĩa:

- Dùng SQL Server Express trên máy local.
- Dùng Windows Authentication.
- Tin certificate local.

## 2. DbContext

`AppDbContext` là trung tâm EF Core.

Nó làm 3 việc:

1. Khai báo bảng qua `DbSet<T>`.
2. Map entity sang table, key, index, relationship.
3. Cung cấp API đọc/ghi database.

Ví dụ:

```csharp
public virtual DbSet<Product> Products { get; set; }
public virtual DbSet<StockIn> StockIns { get; set; }
public virtual DbSet<StockLedger> StockLedgers { get; set; }
```

Mỗi `DbSet` gần tương đương một bảng.

## 3. Entity

Entity là class đại diện cho một bảng.

Ví dụ:

- `Product` -> bảng `Product`
- `StockIn` -> bảng `StockIn`
- `StockInLine` -> bảng `StockInLine`
- `ProductSerial` -> bảng `ProductSerial`
- `WarrantyClaim` -> bảng `WarrantyClaim`

Mỗi object là một dòng dữ liệu.

## 4. OnModelCreating

`OnModelCreating` là nơi cấu hình database.

Ví dụ:

```csharp
entity.HasIndex(e => e.ProductCode, "UX_Product_ProductCode").IsUnique();
```

Ý nghĩa: `ProductCode` không được trùng.

Ví dụ:

```csharp
entity.Property(e => e.DefaultPrice).HasColumnType("decimal(18, 2)");
```

Ý nghĩa: giá tiền lưu bằng decimal, tối đa 18 chữ số, 2 chữ số thập phân.

## 5. Relationship

Relationship mô tả quan hệ giữa bảng.

Ví dụ:

```csharp
entity.HasOne(d => d.Category).WithMany(p => p.Products)
    .HasForeignKey(d => d.CategoryId);
```

Nghĩa là:

- Một `Product` thuộc một `Category`.
- Một `Category` có nhiều `Product`.
- Khóa ngoại nằm ở `Product.CategoryId`.

Các quan hệ cần nhận diện:

- One-to-many: danh mục có nhiều sản phẩm.
- One-to-one: serial có một warranty coverage active.
- Many-to-one: nhiều phiếu nhập cùng một nhà cung cấp.

## 6. Query bằng LINQ

Ví dụ tìm user:

```csharp
var user = db.AppUsers.FirstOrDefault(u => u.Username == username);
```

Ví dụ lọc sản phẩm active:

```csharp
var products = db.Products
    .Where(p => p.IsActive)
    .OrderBy(p => p.DisplayName)
    .ToList();
```

Quan trọng: truy vấn chỉ thật sự chạy khi gọi `ToList`, `FirstOrDefault`, `Count`, `Any`.

## 7. Add, Update, Delete

Thêm:

```csharp
db.Products.Add(product);
db.SaveChanges();
```

Sửa:

```csharp
product.DisplayName = "Tên mới";
db.SaveChanges();
```

Xóa mềm thường tốt hơn xóa thật:

```csharp
product.IsActive = false;
db.SaveChanges();
```

Dự án dùng nhiều cờ `IsActive` để tránh mất dữ liệu lịch sử.

## 8. DbContext factory

Các service thường nhận:

```csharp
private readonly Func<AppDbContext> _contextFactory;
```

Và dùng:

```csharp
using var db = _contextFactory();
```

Lý do:

- App desktop chạy lâu.
- Không nên giữ một `DbContext` sống mãi.
- Mỗi nghiệp vụ dùng context ngắn hạn giúp dữ liệu mới và ít lỗi tracking.

## 9. Transaction

Transaction đảm bảo hoặc tất cả thành công, hoặc tất cả rollback.

Với nghiệp vụ kho, transaction là bắt buộc vì một hành động có thể đụng nhiều bảng:

- `StockIn`
- `StockInLine`
- `StockBalance`
- `ProductSerial`
- `StockLedger`
- `AuditLog`

Nếu ghi phiếu nhập thành công nhưng không tăng tồn kho, dữ liệu sẽ sai. Transaction ngăn lỗi kiểu đó.

## 10. Migration

Migration là lịch sử thay đổi database schema.

Thư mục:

```text
QuanLyHangHoa/Migrations
```

Khi thêm bảng hoặc thêm cột, EF Core tạo migration để cập nhật database.

Lệnh thường gặp:

```powershell
dotnet ef migrations add AddNewTable
dotnet ef database update
```

Trong repo này, khi chỉ học/đọc code, bạn chưa cần chạy migration ngay. Khi tự code lại, hãy học migration sau khi đã hiểu model.

## 11. Index và constraint

Index giúp tìm nhanh. Unique index chống trùng.

Ví dụ quan trọng:

- `UX_Product_ProductCode`: mã sản phẩm không trùng.
- `UX_ProductSerial_SerialNumber`: serial không trùng.
- `UX_StockIn_DocumentCode`: mã phiếu nhập không trùng.
- `UX_WarrantyClaim_ClaimCode`: mã hồ sơ bảo hành không trùng.

Check constraint giới hạn giá trị hợp lệ.

Ví dụ:

```text
StockIn.PurposeCode IN ('Purchase', 'OpeningBalance', 'Adjustment', 'WarrantyReceive')
```

Nghĩa là phiếu nhập chỉ được thuộc các mục đích đã định nghĩa.

## 12. Các bảng lõi cần nhớ

Nhóm danh mục:

- `Product`
- `Category`
- `Brand`
- `Unit`
- `ProductUnit`
- `Supplier`
- `Customer`
- `Warehouse`

Nhóm kho:

- `StockIn`, `StockInLine`
- `StockOut`, `StockOutLine`
- `StockTransfer`, `StockTransferLine`
- `StockAdjustment`, `StockAdjustmentLine`
- `StockCountSession`, `StockCountLine`
- `StockBalance`
- `StockLedger`
- `ProductSerial`

Nhóm hóa đơn:

- `PurchaseInvoice`, `PurchaseInvoiceLine`
- `SalesInvoice`, `SalesInvoiceLine`

Nhóm bảo hành:

- `WarrantyCoverage`
- `WarrantyClaim`

Nhóm hệ thống:

- `AppUser`
- `AuditLog`

## 13. Bài tập EF Core

1. Mở `AppDbContext.cs`, tìm tất cả `DbSet`.
2. Chọn bảng `Product`, đọc phần mapping của nó trong `OnModelCreating`.
3. Tìm 3 unique index và giải thích vì sao cần unique.
4. Viết pseudo-code thêm một sản phẩm mới.
5. Viết pseudo-code tìm serial theo `SerialNumber`.

## 14. Mốc đạt yêu cầu

Bạn đủ EF Core để đi tiếp khi có thể:

- Biết entity nào tương ứng bảng nào.
- Biết `DbSet` dùng để truy vấn bảng.
- Hiểu vì sao cần `SaveChanges`.
- Hiểu transaction trong nghiệp vụ kho.
- Đọc được một relationship cơ bản trong `OnModelCreating`.
