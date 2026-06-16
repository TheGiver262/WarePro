# NHÓM 4: VIEWMODELS — LỚP LOGIC GIAO DIỆN

> Tài liệu này giải thích chi tiết tầng ViewModel: cách MVVM Toolkit hoạt động, pattern chung của các ViewModel, và phân tích sâu `StockInViewModel` — ViewModel phức tạp nhất trong project.

---

## Nguyên tắc thiết kế ViewModel

```
View (XAML) ←binding→ ViewModel ←gọi→ Service ←query→ DB
```

- **ViewModel chỉ xử lý logic giao diện.** Không chứa SQL hay logic nghiệp vụ phức tạp.
- **Không trực tiếp update DB** — gọi Service thay thế.
- **Kế thừa `ObservableObject`** (CommunityToolkit.Mvvm) để hỗ trợ binding tự động.
- **Implement `IRefreshable`** nếu cần tự làm mới khi người dùng quay lại màn hình.

---

## 1. Cơ chế MVVM Toolkit (Source Generator)

### `[ObservableProperty]` — Property tự sinh code

```csharp
[ObservableProperty] private string _searchDocumentCode = string.Empty;
```
MVVM Toolkit **tự động sinh** (tại compile time):
```csharp
// Code được sinh tự động — bạn không thấy trong file, nhưng nó tồn tại
public string SearchDocumentCode
{
    get => _searchDocumentCode;
    set
    {
        if (SetProperty(ref _searchDocumentCode, value))
        {
            OnSearchDocumentCodeChanged(value); // Gọi partial method nếu có
            OnPropertyChanged(nameof(SearchDocumentCode));
        }
    }
}
```
- Khi `SearchDocumentCode` thay đổi → WPF binding tự cập nhật UI.
- `OnSearchDocumentCodeChanged(value)` là **partial method** — bạn có thể tự implement để thêm logic.

### `[NotifyPropertyChangedFor(nameof(X))]` — Báo hiệu property khác thay đổi

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(CanEdit))]
private bool _isPosted;
```
- Khi `IsPosted` thay đổi → MVVM Toolkit **tự động** cũng raise `OnPropertyChanged("CanEdit")`.
- Dùng khi property A phụ thuộc vào property B: `CanEdit = !IsPosted && !IsViewMode`.

### `[RelayCommand]` — Command tự sinh

```csharp
[RelayCommand]
private void SaveDraft() { ... }
```
MVVM Toolkit sinh:
```csharp
public IRelayCommand SaveDraftCommand { get; } = new RelayCommand(SaveDraft);
```
- XAML bind: `<Button Command="{Binding SaveDraftCommand}" />`
- Nếu hàm có tham số: `[RelayCommand] private void ViewDetail(StockIn si)` → sinh `ViewDetailCommand` với `CommandParameter`.

### Partial methods — Hook vào setter

```csharp
partial void OnSearchDocumentCodeChanged(string value)
{
    if (_isInitialized) LoadData(); // Tự tìm kiếm khi gõ
}
```
- `partial void` được sinh tự động trong phần code của MVVM Toolkit.
- Bạn implement phần thân trong file của mình.

---

## 2. `IRefreshable.cs` — Interface làm mới dữ liệu

```csharp
public interface IRefreshable
{
    void RefreshData();
}
```
- Interface này có 1 method duy nhất.
- `MainViewModel.NavigateToView()` kiểm tra `view.DataContext is IRefreshable` → gọi `RefreshData()` mỗi khi người dùng quay lại màn hình đã cache.
- Chỉ các ViewModel cần làm mới data khi quay lại mới implement interface này.

---

## 3. `StockInLineEditor` — ViewModel con cho dòng nhập kho

**Vai trò:** Mỗi dòng trong danh sách chi tiết phiếu nhập là một `StockInLineEditor`. Đây là ViewModel lồng bên trong `StockInViewModel`.

```csharp
public partial class StockInLineEditor : ObservableObject
{
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSerialComplete))]
    private decimal _quantity = 1;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private Unit? _selectedUnit;
    [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
    [ObservableProperty] private ObservableCollection<string> _serialNumbers = new();
    [ObservableProperty][NotifyPropertyChangedFor(nameof(IsSerialComplete))]
    private bool _isSerialRequired;
```

**Computed properties:**
```csharp
public string SerialSummary => SerialNumbers.Count > 0 ? $"{SerialNumbers.Count} Serial" : "Chưa có Serial";
public bool IsSerialComplete => !IsSerialRequired || SerialNumbers.Count == (int)Quantity;
// Hoàn chỉnh serial = không cần serial HOẶC số serial = số lượng
```

### Reactive updates — Chuỗi phản ứng khi chọn sản phẩm

```csharp
partial void OnSelectedProductChanged(Product? value)
{
    if (value != null)
    {
        Price = value.DefaultPrice;        // Tự điền giá mặc định
        IsSerialRequired = value.IsSerialTracked; // Bật/tắt yêu cầu serial
        LoadUnits(value.Id);              // Load đơn vị tính của sản phẩm
        UpdateBaseQuantity();
    }
}
```

```csharp
private void LoadUnits(int productId)
{
    var productUnits = _productUnitService.GetByProductId(productId, includeDefault: true);
    AvailableUnits.Clear();
    foreach (var pu in productUnits) AvailableUnits.Add(pu.Unit);

    // Chọn đơn vị mặc định của sản phẩm
    SelectedUnit = AvailableUnits.FirstOrDefault(u => u.Id == SelectedProduct.DefaultUnitId)
                ?? AvailableUnits.FirstOrDefault();
}
```

### Quy đổi đơn vị

```csharp
partial void OnQuantityChanged(decimal value) => UpdateBaseQuantity();
partial void OnSelectedUnitChanged(Unit? value) => UpdateBaseQuantity();

private void UpdateBaseQuantity()
{
    // Gọi service để quy đổi sang đơn vị cơ bản
    BaseQuantity = _productUnitService.ConvertToBaseUnit(SelectedProduct.Id, SelectedUnit.Id, Quantity);
    // Ví dụ: 2 Hộp × ConversionFactor(6) = BaseQuantity 12 cái
}
```

### Lắng nghe thay đổi trong Collection

```csharp
public StockInLineEditor(ProductUnitService productUnitService)
{
    _productUnitService = productUnitService;
    // Lắng nghe khi SerialNumbers thay đổi (thêm/xóa serial)
    SerialNumbers.CollectionChanged += (s, e) => NotifySerialChanges();
}

public void NotifySerialChanges()
{
    OnPropertyChanged(nameof(SerialSummary));
    OnPropertyChanged(nameof(SerialDetail));
    OnPropertyChanged(nameof(IsSerialComplete)); // Cập nhật trạng thái hoàn chỉnh
}
```

---

## 4. `StockInViewModel` — ViewModel chính của màn hình Nhập kho

### 4.1 Khai báo Fields và Properties

```csharp
private int _skip = 0;           // Vị trí phân trang hiện tại
private const int PageSize = 100; // Số record mỗi trang
private bool _isLoading = false;  // Cờ ngăn load trùng lặp
private bool _isInitialized = false; // Cờ ngăn filter chạy trước khi load xong
```

**Tại sao cần `_isInitialized`?**
```csharp
partial void OnSearchDocumentCodeChanged(string value)
{
    if (_isInitialized) LoadData(); // Chỉ tìm kiếm sau khi VM đã khởi tạo xong
}
```
- Trong constructor, khi gán giá trị cho `SearchDocumentCode = ""`, setter sẽ gọi `OnSearchDocumentCodeChanged("")`.
- Nếu không có `_isInitialized`, sẽ kích hoạt `LoadData()` sớm, trước khi các dependencies sẵn sàng.

**Phân quyền hiển thị:**
```csharp
public bool CanEdit => !IsPosted && !IsViewMode;
// Không edit được nếu: đã ghi sổ HOẶC đang xem (chế độ ReadOnly)

public bool CanApprove => AuthorizationService.CanPerform(_currentUser, PermissionAction.ApproveStock)
                       && (Status == DocumentStatus.Draft);
// Nút "Ghi sổ" chỉ hiện nếu: có quyền ApproveStock VÀ phiếu đang ở trạng thái Draft

public bool CanUserEdit => _currentUser.RoleCode is "Quản trị viên" or "Quản lý" or "Nhân viên kho";
// Kiểm tra role thô (không qua AuthorizationService) — dùng để check quyền sửa phiếu
```

### 4.2 Constructor — Khởi tạo ViewModel

```csharp
public StockInViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
{
    // Fallback: nếu không có user → dùng user system ảo (cho testing)
    _currentUser = currentUser ?? new AppUser { Id=1, Username="System", RoleCode="Quản trị viên" };
    
    // Khởi tạo tất cả Services
    _productService    = new ProductService(factory);
    _stockInService    = new StockInService(factory);
    _productUnitService = new ProductUnitService(factory);
    var refDataService  = new ReferenceDataService(factory);

    // Load các dropdown (sản phẩm, NCC, kho) — đồng bộ, 1 lần duy nhất
    AvailableProducts  = new ObservableCollection<Product>(_productService.GetAllProducts());
    AvailableSuppliers = new ObservableCollection<Supplier>(refDataService.GetAllSuppliers());
    AvailableWarehouses = new ObservableCollection<Warehouse>(refDataService.GetAllWarehouses());

    // Chọn kho mặc định
    SelectedWarehouse = AvailableWarehouses.FirstOrDefault(w => w.IsDefault) ?? AvailableWarehouses.FirstOrDefault();

    // Đăng ký lắng nghe Lines.CollectionChanged để cập nhật TotalAmount khi thêm/xóa dòng
    Lines.CollectionChanged += (s, e) =>
    {
        // Khi thêm dòng: đăng ký lắng nghe thay đổi của dòng đó
        if (e.NewItems != null)
            foreach (StockInLineEditor item in e.NewItems)
                item.PropertyChanged += Line_PropertyChanged;
        // Khi xóa dòng: hủy đăng ký
        if (e.OldItems != null)
            foreach (StockInLineEditor item in e.OldItems)
                item.PropertyChanged -= Line_PropertyChanged;
        OnPropertyChanged(nameof(TotalAmount)); // Cập nhật tổng tiền
    };

    LoadData();          // Load danh sách phiếu
    _isInitialized = true; // Sau khi xong: bật cờ cho phép filter
}
```

### 4.3 `LoadDataAsync(bool reset)` — Load danh sách phiếu

```csharp
private async Task LoadDataAsync(bool reset)
{
    if (_isLoading) return; // Tránh load trùng lặp (đang load thì không load thêm)
    _isLoading = true;
    try
    {
        if (reset) { _skip = 0; StockInList.Clear(); } // Reset phân trang

        // Chạy trên thread nền (không block UI)
        var data = await Task.Run(() => _stockInService.GetStockInPaged(
            SearchDocumentCode, SearchSupplierName, FilterFromDate, FilterToDate,
            warehouseIdFilter, SelectedStatusFilter, _skip, PageSize));

        foreach (var item in data) StockInList.Add(item);
        _skip += data.Count; // Cập nhật vị trí trang tiếp theo

        // Đếm thống kê header (nền)
        await Task.Run(() =>
        {
            var draft  = query.Count(s => s.Status == DocumentStatus.Draft);
            var posted = query.Count(s => s.Status == DocumentStatus.Posted);

            // PHẢI chuyển sang UI thread để cập nhật ObservableCollection
            Application.Current.Dispatcher.Invoke(() =>
            {
                TotalCount  = count;
                DraftCount  = draft;
                PostedCount = posted;
            });
        });
    }
    finally { _isLoading = false; } // Luôn giải phóng cờ dù thành công hay lỗi
}
```

**Tại sao cần `Application.Current.Dispatcher.Invoke()`?**
- WPF chỉ cho phép cập nhật UI từ **UI thread**.
- `Task.Run(...)` chạy trên **thread pool** (thread nền).
- Nếu cập nhật `TotalCount` trực tiếp từ thread nền → WPF throw exception.
- `Dispatcher.Invoke()` → gửi lệnh cập nhật về UI thread.

### 4.4 Điều hướng giữa List View và Detail View

```csharp
// Các cờ điều khiển XAML nào được hiện
[ObservableProperty] private bool _isListViewVisible = true;   // Màn hình danh sách
[ObservableProperty] private bool _isDetailViewVisible = false; // Màn hình chi tiết/soạn

[RelayCommand]
private void CreateNew()
{
    ResetForm();            // Xóa dữ liệu form
    IsListViewVisible = false;
    IsDetailViewVisible = true;
}

[RelayCommand]
private void ViewDetail(StockIn si) // Xem chỉ đọc
{
    IsViewMode = true;      // CanEdit = false
    LoadFromModel(si);      // Điền dữ liệu từ model vào form
    IsListViewVisible = false;
    IsDetailViewVisible = true;
}

[RelayCommand]
private void EditDetail(StockIn si) // Xem để sửa
{
    if (!CanUserEdit) { MessageBox.Show(...); return; }
    if (si.Status == DocumentStatus.Posted) { MessageBox.Show(...); return; }
    IsViewMode = false;     // CanEdit = true
    LoadFromModel(si);
    IsListViewVisible = false;
    IsDetailViewVisible = true;
}

[RelayCommand]
private void BackToList()
{
    IsListViewVisible = true;
    IsDetailViewVisible = false;
    LoadData(); // Làm mới danh sách sau khi quay lại
}
```

### 4.5 `LoadFromModel(StockIn si)` — Điền dữ liệu vào form

```csharp
private void LoadFromModel(StockIn si)
{
    StockInId = si.Id;
    DocumentCode = si.DocumentCode;
    SelectedWarehouse = AvailableWarehouses.FirstOrDefault(w => w.Id == si.WarehouseId);
    SelectedSupplier  = AvailableSuppliers.FirstOrDefault(s => s.Id == si.SupplierId);
    // ... gán các field khác

    Lines.Clear();
    foreach (var line in si.Lines)
    {
        var editor = new StockInLineEditor(_productUnitService)
        {
            SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId) ?? line.Product,
            Quantity = line.Quantity,
            Price    = line.UnitPrice,
        };
        // Load serial đã ghi sổ từ ProductSerials
        foreach (var sn in line.ProductSerials)
            editor.SerialNumbers.Add(sn.SerialNumber);

        Lines.Add(editor);
    }

    // Trigger cập nhật CanEdit, CanApprove sau khi load xong
    OnPropertyChanged(nameof(CanEdit));
    OnPropertyChanged(nameof(CanApprove));
}
```

### 4.6 `OpenSerialInput(StockInLineEditor line)` — Nhập Serial

```csharp
[RelayCommand]
private void OpenSerialInput(StockInLineEditor line)
{
    var existing = string.Join("\n", line.SerialNumbers); // Serials đã có, xuống dòng phân cách
    var isReadOnly = !CanEdit;
    var dialog = new Views.SerialInputWindow(existing, null, isReadOnly); // Mở dialog
    dialog.Owner = Application.Current.MainWindow; // Gắn với cửa sổ cha

    if (CanEdit)
    {
        if (dialog.ShowDialog() == true) // ShowDialog = modal, block cho đến khi đóng
        {
            // Parse input (hỗ trợ range: "SN001-SN005")
            var serials = StockInService.ParseSerialRange(dialog.SerialInput);
            line.SerialNumbers.Clear();
            foreach (var sn in serials) line.SerialNumbers.Add(sn);
            line.Quantity = serials.Count; // Tự cập nhật số lượng = số serial
            line.NotifySerialChanges();
        }
    }
    else
    {
        dialog.ShowDialog(); // Chỉ xem, không cập nhật
    }
}
```

### 4.7 `ConfirmAndPost()` — Ghi sổ (quy trình 3 bước)

```csharp
[RelayCommand]
private void ConfirmAndPost()
{
    // BƯỚC 1: Lưu nháp trước (đảm bảo DB cập nhật)
    if (!ValidateForm()) return;
    var si = CreateModel();
    var siLines = CreateLines();
    _stockInService.SaveDraft(si, siLines, _currentUser.Id);

    // BƯỚC 2: Validate serial trực tiếp trên UI
    foreach (var line in Lines)
    {
        if (line.IsSerialRequired && line.SerialNumbers.Count != (int)line.Quantity)
        {
            var result = MessageBox.Show($"Sản phẩm {line.SelectedProduct?.DisplayName} thiếu serial...",
                "Thiếu Serial", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes) return; // Quay lại nhập thêm
        }
    }

    // BƯỚC 3: Xác nhận với người dùng
    var confirm = MessageBox.Show("Bạn có chắc muốn ghi sổ? Sau khi ghi sổ sẽ không thể chỉnh sửa.",
        "Xác nhận ghi sổ", MessageBoxButton.YesNo);
    if (confirm != MessageBoxResult.Yes) return;

    // BƯỚC 4: Gọi Service Post (transaction thực sự)
    _stockInService.Post(StockInId, _currentUser.Id);
    IsPosted = true;
    Status   = DocumentStatus.Posted;
    OnPropertyChanged(nameof(CanEdit)); // Cập nhật CanEdit = false → disable form
}
```

### 4.8 `ExportExcel()` — Xuất Excel

```csharp
[RelayCommand]
private void ExportExcel()
{
    using var workbook = new ClosedXML.Excel.XLWorkbook(); // Tạo file Excel
    var worksheet = workbook.Worksheets.Add("PhieuNhapKho");

    // Header row (bold + màu nền)
    var headers = new[] { "Mã Phiếu", "Ngày Nhập", ... };
    for (int col = 0; col < headers.Length; col++)
    {
        var cell = worksheet.Cell(1, col + 1);
        cell.Value = headers[col];
        cell.Style.Font.Bold = true;
        cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
    }

    // Data rows
    for (int i = 0; i < StockInList.Count; i++)
    {
        var si = StockInList[i];
        worksheet.Cell(i + 2, 8).Style.NumberFormat.Format = "#,##0"; // Định dạng số tiền
    }

    worksheet.Columns().AdjustToContents(); // Auto-fit cột

    // SaveFileDialog → người dùng chọn nơi lưu
    var saveDialog = new SaveFileDialog { Filter = "Excel Workbook (*.xlsx)|*.xlsx", ... };
    if (saveDialog.ShowDialog() == true)
        workbook.SaveAs(saveDialog.FileName);
}
```

---

## 5. Pattern chung của các ViewModel đơn giản hơn

### DashboardViewModel

```csharp
public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty] private DashboardStats _stats = new();

    public DashboardViewModel(DashboardService dashboardService, MainViewModel mainViewModel)
    {
        _ = LoadAsync(); // Gọi bất đồng bộ ngay trong constructor
    }

    private async Task LoadAsync()
    {
        var result = await Task.Run(() => _dashboardService.GetStatsAsync());
        Application.Current.Dispatcher.Invoke(() => Stats = result);
    }
}
```

### CategoryViewModel (CRUD đơn giản)

```csharp
public partial class CategoryViewModel : ObservableObject, IRefreshable
{
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedCategory;
    [ObservableProperty] private string _searchText = string.Empty;

    [RelayCommand] private void Save() { _service.Save(SelectedCategory, _userId); LoadData(); }
    [RelayCommand] private void Delete() { _service.Delete(SelectedCategory.Id, _userId); LoadData(); }
    public void RefreshData() => LoadData(); // implement IRefreshable
}
```

---

## Tóm tắt các ViewModel theo độ phức tạp

| ViewModel | Màn hình | Độ phức tạp | Đặc điểm |
|---|---|---|---|
| `LoginViewModel` | Đăng nhập | Thấp | 1 command Login |
| `MainViewModel` | Điều hướng | Trung bình | Cache + Navigation |
| `DashboardViewModel` | Tổng quan | Trung bình | Async load, charts |
| `CategoryViewModel` | Danh mục | Thấp | CRUD cơ bản |
| `ProductViewModel` | Sản phẩm | Trung bình | CRUD + tìm kiếm |
| `StockInViewModel` | Nhập kho | **Cao** | Dual-view, serial, phân trang |
| `StockOutViewModel` | Xuất kho | **Cao** | Tương tự StockIn + check Available |
| `StockTransferViewModel` | Chuyển kho | **Cao** | 2 kho, serial tracking |
| `StockCountViewModel` | Kiểm kê | **Cao** | Session workflow phức tạp |
| `WarrantyViewModel` | Bảo hành | **Cao** | Multi-status workflow |
| `ReportViewModel` | Báo cáo | Cao | Nhiều filter + chart |
| `ProductSerialViewModel` | Quản lý Serial | Cao | Serial history tracking |
