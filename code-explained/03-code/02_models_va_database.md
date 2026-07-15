# NHÓM 2: MODELS & DATABASE LAYER

> Tài liệu này giải thích từng Model (ánh xạ bảng DB), mối quan hệ giữa chúng, và vai trò của `AppDbContext`.

---

## Tổng quan kiến trúc dữ liệu

```
[SQL Server: ProductManagementDb]
       ↕ (EF Core ORM)
[AppDbContext] — cầu nối duy nhất
       ↕
[Models/*.cs] — ánh xạ 1-1 với bảng DB
       ↕
[Services/*.cs] — nghiệp vụ
       ↕
[ViewModels/*.cs] — UI logic
```

**Nguyên tắc chốt:** Mọi thao tác DB đều đi qua `AppDbContext`. Không có raw SQL trực tiếp trong ViewModels — chỉ trong Services.

---

## Nhóm 1: Tài khoản & Bảo mật

### `AppUser.cs`

Bảng `AppUser` — tài khoản người dùng hệ thống.

| Property | Kiểu | Ràng buộc | Ý nghĩa |
|---|---|---|---|
| `Id` | int | PK, auto | Khóa chính |
| `Username` | string | UNIQUE | Tên đăng nhập (unique, case-sensitive) |
| `PasswordHash` | string | NOT NULL | Hash BCrypt (không bao giờ là plaintext) |
| `RoleCode` | string | NOT NULL | `"Admin"` hoặc `"Staff"` |
| `FullName` | string | 200 chars | Họ tên hiển thị |
| `MustChangePassword` | bool | | `true` = bắt đổi mật khẩu lần đầu đăng nhập |
| `FailedLoginCount` | int | | Số lần sai liên tiếp (reset sau khi login thành công) |
| `LockoutUntil` | DateTime? | | Null = không bị khóa |
| `IsActive` | bool | | `false` = tài khoản bị vô hiệu hóa |
| `LastLoginAt` | DateTime? | | Lần đăng nhập thành công gần nhất |
| `CreatedBy` | int? | FK → AppUser | User nào tạo tài khoản này (null = seeded) |

**Navigation collections** — User một người có thể liên quan đến nhiều bảng với vai trò khác nhau:
```csharp
public virtual ICollection<StockIn> StockInCreators { get; set; }   // phiếu nhập user TẠO
public virtual ICollection<StockIn> StockInApprovers { get; set; }  // phiếu nhập user PHÊ DUYỆT
public virtual ICollection<StockIn> StockInPosters { get; set; }    // phiếu nhập user GHI SỔ
```
- Pattern **Creator → Approver → Poster** lặp lại cho StockIn, StockOut, StockAdjustment, StockTransfer → đây là workflow 3 bước của phiếu kho.

### `AuditLog.cs`

Bảng `AuditLog` — nhật ký hệ thống, mọi thay đổi quan trọng đều được ghi vào đây.

---

## Nhóm 2: Sản phẩm & Danh mục

### `Product.cs` — Model sản phẩm (quan trọng nhất)

```csharp
public int Id { get; set; }
public string ProductCode { get; set; }     // Mã sản phẩm, ví dụ: "SP001"
public string DisplayName { get; set; }     // Tên hiển thị
public string? Description { get; set; }    // Mô tả (nullable)
public decimal? CostPrice { get; set; }     // Giá vốn (nullable - dùng DefaultPrice nếu null)
public int CategoryId { get; set; }         // FK → Category
public int BrandId { get; set; }            // FK → Brand
public int DefaultUnitId { get; set; }      // FK → Unit (đơn vị tính mặc định)
public decimal DefaultPrice { get; set; }   // Giá bán mặc định
public int WarrantyPeriodMonths { get; set; } // Số tháng bảo hành mặc định
public bool IsSerialTracked { get; set; }   // TRUE = cần quản lý serial riêng từng chiếc
public bool IsActive { get; set; } = true;  // FALSE = sản phẩm đã ngưng kinh doanh
```

**Computed properties** (chỉ tính toán, không lưu vào DB):
```csharp
public string CategoryName => Category?.DisplayName ?? "N/A";  // join tự động qua navigation
public string BrandName    => Brand?.DisplayName ?? "N/A";
public decimal StockQuantity => StockBalances?.Sum(b => b.OnHandQuantity) ?? 0; // tổng tồn kho tất cả kho
public bool IsLowStock => StockQuantity < 10; // ngưỡng cảnh báo hàng ít
public decimal TotalValue  => StockQuantity * (CostPrice ?? DefaultPrice); // giá trị tồn kho
```
- `StockBalances?.Sum(...)` — dùng null-conditional để tránh NullReferenceException. Nếu `StockBalances` chưa được load từ DB (do không Include), trả về `null`, dùng `?? 0`.

**`IsSerialTracked` — flag quan trọng:**
- `true` → sản phẩm như điện thoại, laptop. Mỗi chiếc có Serial Number riêng. Khi nhập/xuất phải nhập đủ serial.
- `false` → sản phẩm như phụ kiện, cáp sạc. Quản lý theo số lượng, không cần serial.

### `ProductSerial.cs` — Số serial từng chiếc

```csharp
public string SerialNumber { get; set; }   // Số serial, ví dụ: "SN12345678"
public string CurrentStatus { get; set; }  // Trạng thái hiện tại
public int? CurrentWarehouseId { get; set; } // Đang ở kho nào (null = đã xuất)
public int LastStockInLineId { get; set; }   // Được nhập vào từ dòng nhập kho nào
public int? LastStockOutLineId { get; set; } // Đã xuất qua dòng xuất kho nào
```

**Setter đặc biệt của `CurrentStatus`:**
```csharp
private string _currentStatus = null!;
public string CurrentStatus
{
    get => _currentStatus;
    set => _currentStatus = string.Equals(value, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase)
        ? "ReturnedToManufacturer"
        : (value ?? string.Empty);
}
```
- Chuẩn hóa value `"ReturnedToManufacturer"` (không phân biệt hoa/thường → luôn lưu đúng case).
- Các value khác được set bình thường, chỉ tránh null.

**Các giá trị `CurrentStatus` hợp lệ:**
| Giá trị | Ý nghĩa |
|---|---|
| `InStock` | Đang trong kho |
| `Sold` | Đã bán ra |
| `InWarranty` | Đang bảo hành |
| `ReturnedToManufacturer` | Đã gửi cho NSX xử lý |
| `Lost` | Mất/hỏng không thu hồi được |
| `Transferred` | Đang chuyển kho |

### `Category.cs`, `Brand.cs`, `Unit.cs`

Model phẳng đơn giản — chỉ có `Id`, `DisplayName`, `IsActive`. Dùng làm danh mục tham chiếu (reference data).

---

## Nhóm 3: Tồn kho — 3 bảng trung tâm

Đây là phần quan trọng nhất của hệ thống. Ba bảng này làm việc cùng nhau để quản lý số lượng hàng.

### `StockBalance.cs` — Số dư tồn kho hiện tại

```csharp
public int WarehouseId { get; set; }       // Kho nào
public int ProductId { get; set; }         // Sản phẩm nào
public decimal OnHandQuantity { get; set; }      // Số lượng vật lý thực tế trong kho
public decimal AvailableQuantity { get; set; }   // Số lượng có thể xuất = OnHand - Reserved
public decimal ReservedQuantity { get; set; }    // Đã được "giữ chỗ" cho đơn đang xử lý
```

**Quan hệ:** 1 bảng `StockBalance` = 1 dòng cho mỗi cặp (Kho × Sản phẩm). Ví dụ: Sản phẩm iPhone 15 trong Kho Hà Nội có 1 dòng riêng.

**Sự khác biệt OnHand vs Available:**
- `OnHand = 10`: Vật lý đang có 10 cái trong kho.
- `Reserved = 3`: Đang có 3 phiếu xuất chưa ghi sổ, "giữ chỗ" 3 cái.
- `Available = 7`: Thực tế chỉ có thể xuất thêm 7 cái (10 - 3).

### `StockLedger.cs` — Sổ cái kho (nguồn sự thật duy nhất)

```csharp
public int WarehouseId { get; set; }         // Kho
public int ProductId { get; set; }           // Sản phẩm
public int? ProductSerialId { get; set; }    // Serial cụ thể (null nếu không track serial)
public string SourceDocumentType { get; set; } // "StockIn", "StockOut", "Adjustment", ...
public int SourceDocumentId { get; set; }    // ID của phiếu gốc (ví dụ: StockIn.Id = 5)
public string MovementType { get; set; }     // "IN" hoặc "OUT"
public decimal Quantity { get; set; }        // Số lượng (luôn dương)
public int PostedBy { get; set; }            // Ai ghi sổ
public DateTime PostedAt { get; set; }       // Ghi sổ lúc nào
```

**Vai trò:** Đây là bản ghi bất biến (immutable) của mọi biến động kho. Không bao giờ sửa hay xóa. Nếu cần đảo ngược, tạo thêm entry ngược lại.

**Ví dụ thực tế:**
```
Id=1: Kho1, ProductA, StockIn/Id=10, "IN",  Qty=100, PostedBy=1, PostedAt=9:00
Id=2: Kho1, ProductA, StockOut/Id=5, "OUT", Qty=20,  PostedBy=2, PostedAt=11:00
```
Tồn kho hiện tại của ProductA tại Kho1 = SUM(IN) - SUM(OUT) = 100 - 20 = 80.

### `StockIn.cs` — Header phiếu nhập kho

```csharp
public string DocumentCode { get; set; }   // Mã phiếu, ví dụ: "NK20240616001"
public int? SupplierId { get; set; }       // Nhà cung cấp (nullable = không có NCC cụ thể)
public int WarehouseId { get; set; }       // Kho nhận hàng
public string PurposeCode { get; set; }    // Mục đích: "Purchase", "Return", "Transfer", ...
public string Status { get; set; }         // "Draft" → "Approved" → "Posted"
public DateTime? ImportDate { get; set; }  // Ngày nhập hàng thực tế
public string? Notes { get; set; }         // Ghi chú
public int CreatedBy { get; set; }         // Ai tạo phiếu
public int? ApprovedBy { get; set; }       // Ai phê duyệt (null = chưa phê duyệt)
public int? PostedBy { get; set; }         // Ai ghi sổ vào StockLedger (null = chưa ghi)
public DateTime CreatedAt { get; set; }
public DateTime? ApprovedAt { get; set; }
public DateTime? PostedAt { get; set; }
```

**Workflow 3 bước của phiếu kho:**
```
[Draft] → (Approve) → [Approved] → (Post) → [Posted]
```
- **Draft:** Đang soạn, có thể sửa/xóa tự do.
- **Approved:** Đã được phê duyệt, không sửa nữa.
- **Posted:** Đã ghi vào `StockLedger` và cập nhật `StockBalance`. Không thể hoàn tác trực tiếp (phải làm phiếu đảo ngược).

### `StockInLine.cs` — Chi tiết dòng phiếu nhập kho

```csharp
public int StockInId { get; set; }      // FK → StockIn (header)
public int ProductId { get; set; }      // Sản phẩm nào
public int UnitId { get; set; }         // Đơn vị tính (hộp, cái, thùng, ...)
public decimal Quantity { get; set; }   // Số lượng theo đơn vị trên
public decimal BaseQuantity { get; set; } // Số lượng quy đổi về đơn vị cơ bản
public decimal UnitPrice { get; set; }  // Đơn giá nhập
public string? DraftSerials { get; set; } // Danh sách serial tạm (JSON string), trước khi Confirm
```

**`DraftSerials` — cơ chế lưu serial tạm:**
```csharp
// Ví dụ giá trị: '["SN001","SN002","SN003"]'
```
- Khi nhân viên nhập serial cho một dòng phiếu nhập, danh sách serial tạm được lưu ở đây (JSON string).
- Khi phiếu được "Post" → `DraftSerials` được parse và tạo thành các record `ProductSerial` chính thức.
- Sau khi Post, trường này không còn ý nghĩa.

### `StockOut.cs` — Header phiếu xuất kho

Tương tự `StockIn.cs` nhưng có một số khác biệt:
```csharp
public int CustomerId { get; set; }     // Khách hàng nhận hàng (bắt buộc)
public string PurposeCode { get; set; } // "Sale", "Transfer", "Warranty", ...
```
- Xuất kho **bắt buộc có khách hàng** (khác nhập kho có thể không có NCC).

---

## Nhóm 4: Bảo hành

### `WarrantyCoverage.cs` — Phiếu bảo hành (hợp đồng)

Phiếu bảo hành được tạo khi bán hàng:
```csharp
public int ProductSerialId { get; set; }  // Serial được bảo hành
public int CustomerId { get; set; }        // Khách hàng
public DateTime StartDate { get; set; }   // Ngày bắt đầu bảo hành
public DateTime EndDate { get; set; }     // Ngày hết bảo hành
public string Status { get; set; }        // "Active", "Expired", "Voided"
```

### `WarrantyClaim.cs` — Yêu cầu bảo hành (sự kiện)

Mỗi khi khách mang hàng đến bảo hành → tạo `WarrantyClaim`:
```csharp
public int WarrantyCoverageId { get; set; }  // FK → phiếu bảo hành
public int ProductSerialId { get; set; }      // Serial đang bảo hành
public int? ReplacementSerialId { get; set; } // Serial thay thế (nếu đổi máy)
public int? ReplacementStockOutId { get; set; } // Phiếu xuất máy thay thế
public string? ProblemDescription { get; set; } // Khách mô tả lỗi
public string? TechnicalConclusion { get; set; } // Kết luận kỹ thuật
public string? ManufacturerResult { get; set; } // Kết quả từ NSX (nếu gửi NSX)
public string Status { get; set; }  // "Pending" → "Approved" → "Sent" → "Returned" / "Closed"
```

**Computed property:**
```csharp
public string CustomerName => WarrantyCoverage?.Customer?.DisplayName ?? string.Empty;
```
- Truy cập `WarrantyCoverage.Customer.DisplayName` qua 2 cấp navigation. Sử dụng `?.` để tránh null.

---

## Nhóm 5: Kho hàng

### `Warehouse.cs` — Kho hàng

```csharp
public string WarehouseCode { get; set; }  // Mã kho, ví dụ: "KHO-HN"
public string DisplayName { get; set; }    // Tên hiển thị
public bool IsDefault { get; set; }        // Kho mặc định khi tạo phiếu mới
public bool IsActive { get; set; }         // Kho đang hoạt động
```

Navigation collections:
```csharp
public virtual ICollection<StockTransfer> StockTransfersFrom { get; set; } // Phiếu chuyển kho ĐI
public virtual ICollection<StockTransfer> StockTransfersTo { get; set; }   // Phiếu chuyển kho ĐẾN
```
- Một kho vừa có thể là kho **gửi** vừa là kho **nhận** trong phiếu chuyển kho.

---

## Mối quan hệ giữa các bảng chính

```
Category ─┐
Brand     ─┤→ Product ──→ StockBalance (OnHand per Warehouse)
Unit      ─┘      │
                  │──→ ProductSerial ──→ StockLedger
                  │         │
                  │         └──→ WarrantyCoverage ──→ WarrantyClaim
                  │
StockIn ──→ StockInLine ──→ ProductSerial (LastStockInLine)
StockOut ──→ StockOutLine ──→ ProductSerial (LastStockOutLine)
```

**Khi "Post" một phiếu nhập kho, các bước xảy ra:**
1. Với mỗi `StockInLine`:
   - Thêm record vào `StockLedger` (MovementType = "IN")
   - Cập nhật `StockBalance.OnHandQuantity += BaseQuantity`
   - Nếu có serial: tạo record `ProductSerial` (CurrentStatus = "InStock")
2. Cập nhật `StockIn.Status = "Posted"`, ghi `PostedBy`, `PostedAt`.

---

## Tóm tắt các bảng theo vai trò

| Nhóm | Bảng | Vai trò |
|---|---|---|
| **Master Data** | Product, Category, Brand, Unit, Warehouse, Supplier, Customer | Danh mục tham chiếu |
| **Tài khoản** | AppUser, AuditLog | Xác thực & kiểm toán |
| **Nhập kho** | StockIn, StockInLine | Header + dòng chi tiết phiếu nhập |
| **Xuất kho** | StockOut, StockOutLine | Header + dòng chi tiết phiếu xuất |
| **Serial** | ProductSerial | Trạng thái từng chiếc hàng |
| **Tồn kho** | StockBalance | Số dư hiện tại (OnHand, Available) |
| **Sổ cái** | StockLedger | Lịch sử biến động bất biến (append-only) |
| **Bảo hành** | WarrantyCoverage, WarrantyClaim | Hợp đồng + yêu cầu bảo hành |
| **Chuyển kho** | StockTransfer, StockTransferLine | Điều chuyển hàng giữa kho |
| **Kiểm kê** | StockCountSession, StockCountLine | Kiểm đếm định kỳ |
| **Hóa đơn** | PurchaseInvoice, SalesInvoice + Lines | Hóa đơn mua/bán tài chính |
