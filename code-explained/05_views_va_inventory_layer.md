# NHÓM 5: VIEWS & INVENTORY LAYER

> Tài liệu này giải thích tầng View (XAML + code-behind) và tầng Inventory — lớp domain thuần túy xử lý cập nhật tồn kho.

---

## PHẦN A: VIEWS (Tầng giao diện)

---

## 1. Vai trò của Views trong MVVM

```
MainWindow.xaml
    └─ ContentControl (bind CurrentView)
            ├─ DashboardView.xaml
            ├─ StockInView.xaml    ← DataContext = StockInViewModel
            ├─ StockOutView.xaml
            └─ ...
```

**Nguyên tắc:** Views gần như không có logic. Toàn bộ logic nằm trong ViewModel. Code-behind (`.xaml.cs`) chỉ xử lý những thứ **không thể làm bằng XAML binding**:
- Event của `PasswordBox` (không hỗ trợ binding)
- Drag & Drop file phức tạp
- Tương tác với UI control chưa hỗ trợ command (DataGrid scroll, ...)

---

## 2. `Views/LoginView.xaml.cs` — Màn hình Đăng nhập

**Đặc điểm duy nhất:** `PasswordBox` của WPF không hỗ trợ binding vì lý do bảo mật (password không được lưu trong memory dưới dạng `string` thông thường).

```csharp
private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
{
    if (this.DataContext != null)
    {
        var vm = (LoginViewModel)this.DataContext;
        vm.Password = txtPassword.Password; // Gán thủ công sang ViewModel
    }
}
```
- Mỗi khi user gõ ký tự → sự kiện `PasswordChanged` kích hoạt → lấy chuỗi từ `PasswordBox.Password` → set vào ViewModel.
- Đây là **workaround phổ biến** trong WPF khi làm việc với `PasswordBox`.

```csharp
private void BtnClose_Click(object sender, RoutedEventArgs e)
{
    Application.Current.Shutdown(); // Đóng toàn bộ app
}
```

---

## 3. `Views/SerialInputWindow.xaml.cs` — Dialog nhập Serial

**Vai trò:** Dialog modal cho phép người dùng nhập danh sách serial. Hỗ trợ 3 chế độ:
1. **Nhập tay**: Gõ trực tiếp vào TextBox (hỗ trợ range như `SN001-SN005`)
2. **Chọn từ danh sách**: Tick checkbox từ danh sách serial có sẵn trong kho (dùng cho xuất kho)
3. **Xem chỉ đọc**: Hiển thị serial đã ghi sổ (không sửa được)

### DependencyProperty — Binding với XAML

```csharp
public static readonly DependencyProperty SerialInputProperty =
    DependencyProperty.Register(nameof(SerialInput), typeof(string), typeof(SerialInputWindow),
        new PropertyMetadata(string.Empty));

public string SerialInput
{
    get => (string)GetValue(SerialInputProperty);
    set => SetValue(SerialInputProperty, value);
}
```
- `DependencyProperty` là cơ chế binding nâng cao của WPF. `SerialInput` có thể được bind từ XAML.
- Sau khi dialog đóng với `DialogResult = true`, caller đọc `dialog.SerialInput` để lấy kết quả.

### Constructor — Khởi tạo dialog

```csharp
public SerialInputWindow(string existingInput = "", IEnumerable<ProductSerial>? available = null, bool isReadOnly = false)
{
    IsReadOnly = isReadOnly;
    _hasAvailableSource = available != null; // Có nguồn serial để chọn không?
    InitializeComponent();

    _isUpdating = true; // Khóa để tránh event chạy vòng lặp
    try
    {
        if (available != null) // Chế độ "chọn từ kho"
        {
            var existingSerials = new HashSet<string>(
                StockInService.ParseSerialRange(existingInput),
                StringComparer.OrdinalIgnoreCase);

            foreach (var s in available)
            {
                AvailableSerials.Add(new AvailableSerialItem
                {
                    SerialNumber = s.SerialNumber,
                    IsSelected = existingSerials.Contains(s.SerialNumber) // Tự tick những serial đã chọn
                });
            }
        }
        SerialInput = existingInput;
        DataContext = this; // Dialog tự là DataContext của mình
        SerialTextBox.Text = existingInput;
    }
    finally { _isUpdating = false; }
    UpdatePreview();
}
```

### Cơ chế 2 chiều: TextBox ↔ Checkbox

**Vấn đề:** Khi gõ serial vào TextBox thì checkbox phải cập nhật; khi tick checkbox thì TextBox phải cập nhật. Nếu không khóa, sẽ tạo vòng lặp vô tận.

**Giải pháp:** Cờ `_isUpdating`:

```csharp
// TextBox → Checkbox
private void SerialTextBox_TextChanged(object sender, TextChangedEventArgs e)
{
    UpdatePreview(); // Cập nhật preview đếm serial

    if (_isUpdating) return; // Nếu đang cập nhật từ Checkbox → bỏ qua
    _isUpdating = true;
    try
    {
        var parsed = new HashSet<string>(StockInService.ParseSerialRange(SerialTextBox.Text));
        foreach (var item in AvailableSerials)
            item.IsSelected = parsed.Contains(item.SerialNumber); // Sync checkbox
        SerialInput = SerialTextBox.Text;
    }
    finally { _isUpdating = false; }
}

// Checkbox → TextBox
private void CheckBox_Click(object sender, RoutedEventArgs e)
{
    if (_isUpdating) return;
    _isUpdating = true;
    try
    {
        if (sender is CheckBox cb && cb.DataContext is AvailableSerialItem item)
        {
            item.IsSelected = cb.IsChecked ?? false;
            UpdateTextBoxFromCheckboxes();
        }
    }
    finally { _isUpdating = false; }
}

private void UpdateTextBoxFromCheckboxes()
{
    var selected = AvailableSerials.Where(x => x.IsSelected).Select(x => x.SerialNumber).ToList();
    SerialTextBox.Text = string.Join(Environment.NewLine, selected); // Cập nhật TextBox
    SerialInput = SerialTextBox.Text;
}
```

### Trả kết quả về caller

```csharp
private void Confirm_Click(object sender, RoutedEventArgs e)
{
    DialogResult = true; // Báo hiệu dialog đóng với xác nhận
    Close();
}

private void Cancel_Click(object sender, RoutedEventArgs e)
{
    DialogResult = false; // Hủy bỏ
    Close();
}
```

**Caller nhận kết quả:**
```csharp
if (dialog.ShowDialog() == true) // true = người dùng bấm Xác nhận
{
    var serials = StockInService.ParseSerialRange(dialog.SerialInput);
    // Sử dụng serials...
}
```

---

## 4. Các code-behind phổ biến nhất

### `StockInView.xaml.cs` và `StockOutView.xaml.cs`

Gần như chỉ override một sự kiện:
```csharp
// Khi DataGrid scroll xuống đáy → load thêm phiếu (vô hạn cuộn)
private void StockInDataGrid_ScrollChanged(object sender, ScrollChangedEventArgs e)
{
    if (e.VerticalOffset > 0 && e.VerticalOffset >= e.ExtentHeight - e.ViewportHeight - 20)
    {
        if (DataContext is StockInViewModel vm)
            vm.LoadMoreCommand.Execute(null);
    }
}
```

### `PurchaseInvoiceView.xaml.cs` và `SalesInvoiceView.xaml.cs`

Xử lý tương tự nhưng có thêm logic cho invoice-specific events.

---

## PHẦN B: INVENTORY LAYER — Tầng Domain Thuần túy

---

## 5. Tổng quan kiến trúc Inventory Layer

```
StockInService.Post() → InventoryPostingService.PostStockIn()
                                    ↓
                        IInventoryUnitOfWork (interface)
                                    ↓
                        EfInventoryUnitOfWork (implementation EF Core)
                                    ↓
                        SQL Server (StockLedger, StockBalance, ProductSerial)
```

**Tại sao tách thành layer riêng?**
- **Testability:** `InventoryPostingService` chỉ phụ thuộc vào interfaces (`IInventoryUnitOfWork`, `IClock`) → có thể test mà không cần DB thật.
- **Separation of Concerns:** Logic "cập nhật tồn kho" tách khỏi logic "quản lý phiếu". Service layer xử lý workflow phiếu; Inventory layer xử lý số liệu kho.

---

## 6. `Inventory/InventoryPorts.cs` — Định nghĩa Interfaces

```csharp
public interface IInventoryUnitOfWork
{
    ProductSnapshot GetProduct(int productId);             // Lấy thông tin sản phẩm
    StockBalanceSnapshot GetOrCreateBalance(int productId, int warehouseId); // Lấy/tạo số dư kho
    void SaveBalance(StockBalanceSnapshot balance);        // Cập nhật số dư
    bool SerialExists(string serialNumber);               // Kiểm tra serial đã tồn tại chưa
    void SaveSerial(ProductSerialSnapshot serial);         // Lưu serial mới
    void AddLedger(StockLedgerEntry entry);               // Ghi vào sổ cái
    void AddAudit(AuditLogEntry entry);                   // Ghi audit log
    void MarkDocumentPosted(int documentId, string type); // Đánh dấu phiếu đã ghi sổ
    void Commit();                                         // Thực thi tất cả thay đổi
}

public interface IClock
{
    DateTime Now { get; } // Abstraction cho thời gian → dễ test (inject fake time)
}
```

**Lý do dùng `IClock`:**
- Trong unit test, có thể inject `FakeClock { Now = new DateTime(2024,1,1) }` để kiểm soát thời gian.
- Code production dùng `SystemClock { Now => DateTime.Now }`.

---

## 7. `Inventory/InventoryPostingService.cs` — Logic cập nhật tồn kho

**Vai trò:** Đây là class thuần túy xử lý nghiệp vụ tồn kho. Không phụ thuộc EF Core, không phụ thuộc WPF.

### `PostStockIn(PostStockInCommand command)`

```csharp
public void PostStockIn(PostStockInCommand command)
{
    // 1. Validate trạng thái
    if (command.Status != StockDocumentStatus.Approved && command.Status != StockDocumentStatus.Posted)
        throw new InventoryDomainException("Only approved documents can be posted.");

    if (command.Quantity <= 0)
        throw new InventoryDomainException("Stock-in quantity must be greater than zero.");

    // 2. Validate serial
    var serialNumbers = command.SerialNumbers.Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    EnsureNoDuplicateSerials(serialNumbers); // Kiểm tra trùng trong command

    if (product.IsSerialTracked && serialNumbers.Length != command.Quantity)
        throw new InventoryDomainException("Serial count must match stock-in quantity.");

    foreach (var serialNumber in serialNumbers)
    {
        if (_unitOfWork.SerialExists(serialNumber))
            throw new InventoryDomainException($"Serial {serialNumber} already exists.");
    }

    // 3. CẬP NHẬT StockBalance (OnHand + Available)
    var balance = _unitOfWork.GetOrCreateBalance(command.ProductId, warehouseId);
    _unitOfWork.SaveBalance(balance with
    {
        OnHandQuantity    = balance.OnHandQuantity + (int)command.Quantity,
        AvailableQuantity = balance.AvailableQuantity + (int)command.Quantity
    });

    // 4. TẠO ProductSerial records
    foreach (var serialNumber in serialNumbers)
    {
        _unitOfWork.SaveSerial(new ProductSerialSnapshot(
            serialNumber, command.ProductId, warehouseId, SerialStatus.InStock));
    }

    // 5. GHI StockLedger (sổ cái bất biến)
    _unitOfWork.AddLedger(new StockLedgerEntry(
        command.DocumentId, command.ProductId, warehouseId,
        StockLedgerDirection.In, (int)command.Quantity, _clock.Now, command.PostedByUserId));

    // 6. GHI AuditLog
    _unitOfWork.AddAudit(new AuditLogEntry(
        command.DocumentId, AuditActionCode.PostStockIn, _clock.Now, command.PostedByUserId));

    // 7. Đánh dấu phiếu Posted
    _unitOfWork.MarkDocumentPosted(command.DocumentId, "StockIn");

    // 8. COMMIT — thực thi tất cả vào DB
    _unitOfWork.Commit();
}
```

**Cú pháp `record with { ... }`:**
```csharp
_unitOfWork.SaveBalance(balance with
{
    OnHandQuantity    = balance.OnHandQuantity + quantity,
    AvailableQuantity = balance.AvailableQuantity + quantity
});
```
- `balance` là C# `record` (immutable). Cú pháp `with { ... }` tạo bản sao mới với các field được chỉ định thay đổi.
- Balance cũ không bị modify → pattern functional/immutable.

---

## 8. `Inventory/EfInventoryUnitOfWork.cs` — Kết nối interface với EF Core

```csharp
public class EfInventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly AppDbContext _context;

    public ProductSnapshot GetProduct(int productId)
    {
        var product = _context.Products.Find(productId)
            ?? throw new InventoryDomainException($"Product {productId} not found.");
        return new ProductSnapshot(product.Id, product.IsSerialTracked);
    }

    public StockBalanceSnapshot GetOrCreateBalance(int productId, int warehouseId)
    {
        var balance = _context.StockBalances
            .FirstOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);

        if (balance == null) // Chưa có → tạo mới
        {
            var newBalance = new StockBalance { ProductId = productId, WarehouseId = warehouseId };
            _context.StockBalances.Add(newBalance);
            _context.SaveChanges();
            return new StockBalanceSnapshot(productId, warehouseId, 0, 0, 0);
        }

        return new StockBalanceSnapshot(productId, warehouseId,
            balance.OnHandQuantity, balance.AvailableQuantity, balance.ReservedQuantity);
    }

    public void Commit()
    {
        _context.SaveChanges(); // Thực thi tất cả pending changes vào DB
    }
}
```

---

## 9. `Inventory/InventoryRecords.cs` — Data Transfer Objects (Records)

```csharp
// C# record: immutable, value equality, concise syntax
public sealed record ProductSnapshot(int ProductId, bool IsSerialTracked);
public sealed record StockBalanceSnapshot(int ProductId, int WarehouseId,
    decimal OnHandQuantity, decimal AvailableQuantity, decimal ReservedQuantity);
public sealed record ProductSerialSnapshot(string SerialNumber, int ProductId,
    int? WarehouseId, SerialStatus Status);
public sealed record StockLedgerEntry(int DocumentId, int ProductId, int WarehouseId,
    StockLedgerDirection Direction, int Quantity, DateTime PostedAt, int PostedByUserId);
```

**Tại sao dùng `record` thay vì `class`?**
- **Immutable by default:** Không thể vô tình modify sau khi tạo.
- **Value equality:** `new ProductSnapshot(1, true) == new ProductSnapshot(1, true)` trả về `true` (khác `class` so sánh reference).
- **Concise:** `record` tự sinh constructor, `ToString()`, `Equals()`.

---

## 10. `Inventory/DocumentStatus.cs` — Hằng số trạng thái phiếu

```csharp
public static class DocumentStatus
{
    public const string Draft  = "Draft";
    public const string Posted = "Posted";
}
```
- Dùng `const string` thay vì `enum` để có thể lưu trực tiếp vào cột VARCHAR trong DB mà không cần convert.
- Tránh magic string rải rác trong code: dùng `DocumentStatus.Draft` thay vì `"Draft"`.

---

## Sơ đồ tổng quan: Khi nhấn nút "Ghi sổ"

```
[User bấm nút "Xác nhận ghi sổ" trên StockInView]
        ↓
StockInViewModel::ConfirmAndPostCommand
    1. ValidateForm() — kiểm tra dữ liệu UI
    2. SaveDraft()    — lưu dữ liệu mới nhất vào DB
    3. MessageBox     — xác nhận với user
    4. StockInService.Post(stockInId, userId)
            ↓
        using var transaction = db.BeginTransaction()
        Validate serials (DB + duplicate)
        stockIn.Status = "Posted"
        db.SaveChanges()
        InventoryPostingService.PostStockIn(command) cho từng dòng
                ↓
            IInventoryUnitOfWork (EfInventoryUnitOfWork)
            a. GetOrCreateBalance() — lấy/tạo số dư
            b. SaveBalance()        — cộng OnHand + Available
            c. SaveSerial() × N     — tạo ProductSerial records
            d. AddLedger()          — ghi StockLedger (bất biến)
            e. AddAudit()           — ghi audit log
            f. Commit() → SaveChanges()
        ← Bind LastStockInLineId cho serials
        db.SaveChanges()
        AddAudit(db, "UPDATE", ...)
        transaction.Commit()  ← Toàn bộ commit vào SQL Server
    5. IsPosted = true → CanEdit = false → UI lock form
    6. MessageBox thành công
```

---

## Tóm tắt nhanh

| File / Layer | Vai trò |
|---|---|
| `Views/*.xaml` | XAML khai báo giao diện, bind vào ViewModel |
| `Views/*.xaml.cs` | Code-behind: chỉ xử lý những gì XAML không làm được |
| `SerialInputWindow.xaml.cs` | Dialog nhập serial, hỗ trợ range + checkbox |
| `Inventory/InventoryPorts.cs` | Định nghĩa interface (IInventoryUnitOfWork, IClock) |
| `Inventory/InventoryPostingService.cs` | Logic cập nhật tồn kho (pure domain, không phụ thuộc EF/WPF) |
| `Inventory/EfInventoryUnitOfWork.cs` | Implement interface bằng EF Core |
| `Inventory/InventoryRecords.cs` | DTOs dạng record (immutable) truyền dữ liệu giữa các layer |
| `Inventory/DocumentStatus.cs` | Hằng số trạng thái phiếu (Draft, Posted) |
