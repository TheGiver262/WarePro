# NHÓM 3: SERVICES — LỚP NGHIỆP VỤ

> Tài liệu này giải thích chi tiết tầng Service: các class chứa toàn bộ logic nghiệp vụ, là trung gian giữa ViewModels (UI) và AppDbContext (DB).

---

## Nguyên tắc thiết kế Services

```
ViewModel → Service → AppDbContext → SQL Server
```

- **ViewModel KHÔNG bao giờ query DB trực tiếp.** Mọi thao tác DB đều ủy thác cho Service.
- **Service KHÔNG biết đến UI.** Service không tham chiếu đến WPF, XAML hay MessageBox.
- **Mỗi Service nhận `Func<AppDbContext>` (factory)**, không nhận `AppDbContext` cố định → tránh connection leak.

---

## 1. `AuthorizationService.cs` — Phân quyền

**Vai trò:** Kiểm tra user có quyền thực hiện một hành động không. Đây là lớp static (không cần khởi tạo).

### Cấu trúc phân quyền

```csharp
public enum PermissionAction
{
    ManageUsers, PostStockIn, PostStockOut, PostStockAdjustment,
    CreatePurchaseInvoice, CreateSalesInvoice, CreateWarrantyClaim,
    ViewReports, ManageMasterData, ManageAuditLogs, ApproveStock
}
```

```csharp
private static readonly Dictionary<string, HashSet<PermissionAction>> RolePermissions = new()
{
    ["Quản trị viên"]      = AllPermissions(),                              // TẤT CẢ quyền
    ["Quản lý"]            = AllPermissionsExcept(PermissionAction.ManageUsers), // Không quản lý user
    ["Nhân viên bảo hành"] = { CreateWarrantyClaim, ViewReports },
    ["Nhân viên bán hàng"] = { CreateSalesInvoice, ViewReports },
    ["Nhân viên kho"]      = { PostStockIn, PostStockOut, PostStockAdjustment, ViewReports }
};
```

### Hàm `CanPerform(user, action)`

```csharp
public static bool CanPerform(AppUser? user, PermissionAction action)
{
    if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.RoleCode))
        return false;  // null hoặc tài khoản bị khóa = không có quyền gì

    return RolePermissions.TryGetValue(user.RoleCode, out var permissions)
        && permissions.Contains(action);
}
```
- Hàm static → gọi trực tiếp: `AuthorizationService.CanPerform(user, PermissionAction.PostStockIn)`.
- Nếu `RoleCode` không có trong Dictionary (ví dụ: role cũ bị xóa) → trả về `false` (deny by default).

**Nơi sử dụng trong MainViewModel:**
```csharp
public bool IsAdmin => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageUsers);
public bool CanViewLogs => AuthorizationService.CanPerform(CurrentUser, PermissionAction.ManageAuditLogs);
```
- XAML bind vào `IsAdmin` để ẩn/hiện menu item.

---

## 2. `AuthenticationService.cs` — Xác thực đăng nhập

(Đã giải thích chi tiết ở Nhóm 1. Tóm tắt ở đây để tham chiếu chéo.)

| Hàm | Tham số | Trả về | Mô tả |
|---|---|---|---|
| `Authenticate()` | username, password | `LoginResult` | BCrypt verify, lockout |
| `ChangePassword()` | userId, currentPwd, newPwd | void / throws | Đổi mật khẩu có xác minh |

---

## 3. `StockInService.cs` — Nghiệp vụ nhập kho (phức tạp nhất)

**Vai trò:** Xử lý toàn bộ vòng đời phiếu nhập kho: tạo nháp, lưu, ghi sổ, xóa.

### 3.1 `GetAll()` và `GetStockInPaged(...)` — Đọc dữ liệu

```csharp
public virtual List<StockIn> GetAll()
{
    using var db = _contextFactory();
    return db.StockIns.AsNoTracking()
        .Include(s => s.Supplier)       // JOIN bảng Supplier
        .Include(s => s.Creator)        // JOIN bảng AppUser (người tạo)
        .Include(s => s.Warehouse)      // JOIN bảng Warehouse
        .Include(s => s.Lines)          // JOIN bảng StockInLine
            .ThenInclude(d => d.Product) // JOIN bảng Product qua Lines
        .Include(s => s.Lines)
            .ThenInclude(d => d.ProductSerials) // JOIN bảng ProductSerial qua Lines
        .OrderByDescending(s => s.CreatedAt)
        .ToList();
}
```

**Các kỹ thuật quan trọng:**
- `AsNoTracking()` → EF Core không theo dõi thay đổi của các object trả về → **tối ưu tốc độ đọc**, tiết kiệm bộ nhớ. Dùng khi chỉ cần hiển thị, không cần cập nhật.
- `Include(...)` → EF Core tự sinh câu SQL JOIN để load dữ liệu quan hệ trong 1 query.
- `ThenInclude(...)` → JOIN cấp 2: `StockIn → Lines → Product`.

**`GetStockInPaged(...)` — Phân trang có lọc:**
```csharp
private IQueryable<StockIn> ApplyStockInFilters(IQueryable<StockIn> query, ...)
{
    if (!string.IsNullOrWhiteSpace(code))
        query = query.Where(s => s.DocumentCode.Contains(term)); // LIKE '%term%'

    if (endDate.HasValue)
    {
        var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1); // cuối ngày 23:59:59.9999999
        query = query.Where(s => s.ImportDate <= endOfDay);
    }
    // ...
}

return query
    .OrderByDescending(s => s.ImportDate)
    .Skip(skip)   // bỏ qua N record đầu
    .Take(take)   // lấy N record tiếp theo
    .ToList();    // thực thi SQL
```
- `endDate.Value.Date.AddDays(1).AddTicks(-1)` → kỹ thuật lấy thời điểm **cuối ngày** (23:59:59.9999999). Dùng `AddDays(1).AddTicks(-1)` thay vì `.AddHours(23).AddMinutes(59)...` để chính xác hơn.
- Query được build dần theo điều kiện lọc. SQL chỉ được thực thi khi gọi `.ToList()` ở cuối (Deferred Execution).

### 3.2 `SaveDraft(stockIn, lines, userId)` — Lưu phiếu nháp

**Luồng xử lý:**

```csharp
// BƯỚC 1: Tính toán serials và BaseQuantity
foreach (var line in lines)
{
    var serials = line.ProductSerials?.Select(ps => ps.SerialNumber.Trim())...
    line.DraftSerials = serials.Any() ? string.Join(",", serials) : null;
    line.ProductSerials = new List<ProductSerial>(); // Không để EF tự xử lý serial

    // Quy đổi đơn vị
    var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
    line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
    // Ví dụ: nhập 2 "Hộp" (1 hộp = 6 cái) → BaseQuantity = 2 * 6 = 12
}
```

```csharp
// BƯỚC 2a: Nếu đã tồn tại (UPDATE)
if (existing != null)
{
    if (existing.Status == DocumentStatus.Posted) throw new Exception("Không thể cập nhật phiếu đã ghi sổ.");

    var beforeJson = Serialize(existing); // chụp trạng thái TRƯỚC khi sửa (dùng cho audit)

    // Xóa toàn bộ dòng cũ và serial tạm
    db.ProductSerials.RemoveRange(existing.Lines.SelectMany(l => l.ProductSerials));
    db.StockInLines.RemoveRange(existing.Lines);
    existing.Lines = lines; // thêm dòng mới

    db.SaveChanges();
    AddAudit(db, "UPDATE", existing.Id, beforeJson, afterJson, userId);
}
```

```csharp
// BƯỚC 2b: Nếu chưa tồn tại (CREATE)
else
{
    stockIn.CreatedBy = userId;
    stockIn.CreatedAt = DateTime.Now;
    stockIn.Status = DocumentStatus.Draft;
    stockIn.DocumentCode = $"SI-{DateTime.Now:yyyyMMddHHmmss}"; // tạo mã phiếu tự động
    stockIn.WarehouseId = new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId(); // kho mặc định

    db.StockIns.Add(stockIn);
    db.SaveChanges();
    AddAudit(db, "CREATE", stockIn.Id, null, afterJson, userId);
}
```

### 3.3 `Post(stockInId, userId)` — Ghi sổ (hành động quan trọng nhất)

Đây là hàm phức tạp và quan trọng nhất trong toàn bộ project. Nó thực sự "xác nhận" phiếu nhập kho vào tồn kho.

```csharp
public virtual void Post(int stockInId, int userId)
{
    using var db = _contextFactory();
    using var transaction = db.Database.BeginTransaction(); // BẮT ĐẦU TRANSACTION
```
- `BeginTransaction()` → toàn bộ các thao tác DB trong hàm này được bọc trong một transaction.
- Nếu bất kỳ bước nào thất bại → `transaction.Rollback()` tự động được gọi, không có dữ liệu nào bị ghi nửa vời.

**Bước 1: Validate serial (kiểm tra tính hợp lệ)**

```csharp
// Parse serials từ DraftSerials
var serials = line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries)...

// Kiểm tra số lượng serial khớp với số lượng
if (serials.Count != (int)line.Quantity)
    throw new Exception($"Sản phẩm {product.DisplayName} yêu cầu {Qty} serial, nhưng hiện có {count}.");

// Kiểm tra serial chưa tồn tại trong DB
var existingDbSerials = db.ProductSerials.Where(ps => serials.Contains(ps.SerialNumber)).ToList();
if (existingDbSerials.Any())
    throw new Exception($"Số serial [{...}] đã tồn tại trong hệ thống.");

// Kiểm tra serial không trùng nhau trong phiếu hiện tại
var duplicateDocumentSerials = allDocumentSerials.GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
    .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
if (duplicateDocumentSerials.Any())
    throw new Exception($"Các số serial sau bị trùng lặp trong phiếu: [{...}].");
```

**Bước 2: Đánh dấu phiếu là "Posted"**

```csharp
stockIn.Status = DocumentStatus.Posted;
stockIn.PostedBy = userId;
stockIn.PostedAt = DateTime.Now;
db.SaveChanges();
```

**Bước 3: Gọi `InventoryPostingService` để cập nhật kho**

```csharp
var postingService = new InventoryPostingService(
    new EfInventoryUnitOfWork(db),
    new DbDefaultWarehouseProvider(db),
    new SystemClock());

foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId)) // sắp xếp để tránh deadlock
{
    postingService.PostStockIn(new PostStockInCommand(
        stockIn.Id, stockIn.WarehouseId, StockInKind.Purchase, StockDocumentStatus.Posted,
        line.ProductId, (int)line.BaseQuantity, serials, userId));
}
```
- **Sắp xếp theo `ProductId`** → kỹ thuật tránh deadlock khi nhiều transaction cùng lock record theo thứ tự khác nhau.
- `InventoryPostingService.PostStockIn()` → thực sự ghi vào `StockLedger` và cập nhật `StockBalance`. Xem Nhóm 5 (Inventory layer).

**Bước 4: Liên kết serial với dòng phiếu nhập**

```csharp
foreach (var line in stockIn.Lines)
{
    var dbSerials = db.ProductSerials.Where(ps => serials.Contains(ps.SerialNumber)).ToList();
    foreach (var s in dbSerials)
        s.LastStockInLineId = line.Id; // ghi nhớ serial này đến từ dòng nào của phiếu nào
}
db.SaveChanges();
```

**Bước 5: Commit**

```csharp
transaction.Commit();
```
- Nếu đến đây thành công → toàn bộ thay đổi được commit vào DB.
- Nếu có exception ở bất kỳ bước nào trước → `using` block tự động gọi `transaction.Dispose()` → rollback.

### 3.4 `ParseSerialRange(input)` — Hàm tiện ích parse range serial

```csharp
public static List<string> ParseSerialRange(string input)
```

**Mục đích:** Cho phép nhập serial dạng range thay vì từng cái.

**Regex pattern:** `^(.+?)(\d+)-[^\d]*(\d+)$`

**Ví dụ:**
```
Input:  "SN0001-SN0005"  → ["SN0001","SN0002","SN0003","SN0004","SN0005"]
Input:  "SN001, SN003"   → ["SN001", "SN003"]  (dấu phẩy phân cách)
Input:  "IMEI123456-IMEI123460" → ["IMEI123456", "IMEI123457", ..., "IMEI123460"]
```

```csharp
var rangeMatch = Regex.Match(trimmed, @"^(.+?)(\d+)-[^\d]*(\d+)$");
if (rangeMatch.Success)
{
    string prefix = rangeMatch.Groups[1].Value;  // "SN"
    string startStr = rangeMatch.Groups[2].Value; // "0001"
    string endStr   = rangeMatch.Groups[3].Value; // "0005"
    int padLen = startStr.Length;                 // 4 (giữ padding "0001" thay vì "1")
    for (long i = start; i <= end; i++)
        result.Add(prefix + i.ToString().PadLeft(padLen, '0'));
}
```

### 3.5 `Delete(id, userId)` — Xóa phiếu

```csharp
if (stockIn.Status == DocumentStatus.Posted) throw new Exception("Không thể xóa phiếu đã ghi sổ.");

db.StockInLines.RemoveRange(stockIn.Lines); // xóa chi tiết trước (tránh FK constraint)
db.StockIns.Remove(stockIn);               // xóa header
db.SaveChanges();
AddAudit(db, "DELETE", id, beforeJson, null, userId);
```
- **Chỉ xóa được phiếu Draft**. Phiếu đã ghi sổ → phải làm phiếu đảo ngược.

### 3.6 Kỹ thuật Audit (ghi nhật ký)

```csharp
private string Serialize(StockIn s) // Chụp trạng thái thành JSON
{
    return JsonSerializer.Serialize(new { s.Id, s.DocumentCode, ...,
        Lines = s.Lines?.Select(l => new { l.ProductId, l.Quantity, l.DraftSerials }).ToList()
    });
}

private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
{
    db.AuditLogs.Add(new AuditLog {
        EntityName = "StockIn", EntityId = entityId,
        ActionCode = action,    // "CREATE", "UPDATE", "DELETE"
        BeforeJson = before,    // JSON trước khi sửa (null khi CREATE)
        AfterJson  = after,     // JSON sau khi sửa (null khi DELETE)
        PerformedBy = performedBy,
        PerformedAt = DateTime.Now
    });
    db.SaveChanges();
}
```
- **Before/After JSON** → lưu snapshot trạng thái trước và sau mỗi thay đổi. Nhật ký hệ thống có thể hiển thị "diff" giữa 2 JSON này để biết chính xác đã thay đổi gì.

---

## 4. `DashboardService.cs` — Dữ liệu thống kê

**Vai trò:** Cung cấp tất cả số liệu cho màn hình Dashboard.

### DTOs (Data Transfer Objects) nội bộ

```csharp
public class DashboardStats      // Số liệu tổng hợp
public class RecentActivity      // Hoạt động gần đây
public class RevenueExpenseData  // Dữ liệu biểu đồ doanh thu/chi phí theo tháng
public class InventoryStructureData  // Cơ cấu tồn kho theo danh mục
public class TopSellingProductData   // Sản phẩm bán chạy
public class StockMovementData       // Biến động nhập/xuất kho theo ngày
```

### `GetStatsAsync()` — Truy vấn bất đồng bộ

```csharp
public async Task<DashboardStats> GetStatsAsync()
{
    using var context = _contextFactory();
    var now = DateTime.Now;
    var startOfMonth = new DateTime(now.Year, now.Month, 1);
    var startOfYear  = new DateTime(now.Year, 1, 1);

    // Tổng tồn kho
    stats.TotalInventoryCount = (int)await context.StockBalances.SumAsync(sb => sb.OnHandQuantity);

    // Số phiếu nhập tháng này
    stats.StockInMonthCount = await context.StockIns.CountAsync(s => s.CreatedAt >= startOfMonth);

    // Doanh thu năm (load 1 lần → tính tháng bằng LINQ in-memory)
    var salesYear = await context.SalesInvoices
        .Where(s => s.InvoiceDate >= startOfYear)
        .Select(s => new { s.InvoiceDate, s.GrandTotal })
        .ToListAsync();
    stats.RevenueYear  = salesYear.Sum(s => s.GrandTotal);
    stats.RevenueMonth = salesYear.Where(s => s.InvoiceDate >= startOfMonth).Sum(s => s.GrandTotal);
```
- `async/await` → không block UI thread khi đang query DB.
- **Pattern tối ưu:** Load `salesYear` 1 lần từ DB, sau đó lọc tháng trong bộ nhớ thay vì chạy 2 query riêng.

---

## 5. Các Service đơn giản (Reference Data)

Các service sau có cấu trúc rất đơn giản và nhất quán:
`CategoryService`, `BrandService`, `UnitService`, `SupplierService`, `CustomerService`

**Pattern chung:**
```csharp
public class CategoryService
{
    private readonly Func<AppDbContext> _contextFactory;

    public List<Category> GetAll()
    {
        using var db = _contextFactory();
        return db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.DisplayName).ToList();
    }

    public void Save(Category category, int userId)
    {
        using var db = _contextFactory();
        if (category.Id == 0)
            db.Categories.Add(category);
        else
            db.Entry(category).State = EntityState.Modified;
        db.SaveChanges();
    }

    public void Delete(int id, int userId) { ... }
}
```
- `db.Entry(category).State = EntityState.Modified` → báo EF Core object này đã thay đổi, cần UPDATE.
- `Where(c => c.IsActive)` → mặc định chỉ lấy bản ghi active.

---

## Tóm tắt Services theo chức năng

| Service | Chức năng chính | Độ phức tạp |
|---|---|---|
| `AuthenticationService` | Login, change password, lockout | Trung bình |
| `AuthorizationService` | Role-based permission check (static) | Thấp |
| `StockInService` | CRUD phiếu nhập kho + ghi sổ | **Cao** |
| `StockOutService` | CRUD phiếu xuất kho + ghi sổ | **Cao** |
| `StockTransferService` | CRUD phiếu chuyển kho + ghi sổ | Cao |
| `StockAdjustmentService` | CRUD phiếu điều chỉnh + ghi sổ | Trung bình |
| `StockCountService` | Quản lý phiên kiểm kê | Trung bình |
| `WarrantyClaimService` | Xử lý yêu cầu bảo hành | **Cao** |
| `InvoiceService` | Hóa đơn mua/bán | Trung bình |
| `DashboardService` | Thống kê, chart data | Trung bình |
| `ProductService` | CRUD sản phẩm + tìm kiếm | Trung bình |
| `ProductSerialService` | Quản lý serial | Thấp |
| `CategoryService`, `BrandService`, ... | CRUD danh mục tham chiếu | Thấp |
| `ReferenceDataService` | Load các dropdown (kho, đơn vị, ...) | Thấp |
| `OpeningBalanceImportService` | Import tồn đầu kỳ từ Excel | Trung bình |
| `ReportTraceService / AuditLogService` | Truy vet san pham/serial va nhat ky he thong | Trung bình |
