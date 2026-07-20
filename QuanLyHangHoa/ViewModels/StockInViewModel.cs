using System;
using System.Collections.Generic;
using QuanLyHangHoa.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockInLineEditor : ObservableObject
    {
        private readonly ProductUnitService _productUnitService;

        [ObservableProperty] private int _id;
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSerialComplete))] private decimal _quantity = 1;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private Unit? _selectedUnit;
        [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
        [ObservableProperty] private ObservableCollection<string> _serialNumbers = new();
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSerialComplete))] private bool _isSerialRequired;
        [ObservableProperty] private decimal _baseQuantity;

        public string SerialSummary => SerialNumbers.Count > 0 ? $"{SerialNumbers.Count} Serial" : "Chưa có Serial";
        public string SerialDetail => SerialNumbers.Count > 0 ? string.Join(", ", SerialNumbers) : "";
        public bool IsSerialComplete => !IsSerialRequired || SerialNumbers.Count == (int)Quantity;

        public StockInLineEditor(ProductUnitService productUnitService)
        {
            _productUnitService = productUnitService;
            SerialNumbers.CollectionChanged += (s, e) => NotifySerialChanges();
        }

        public void NotifySerialChanges()
        {
            OnPropertyChanged(nameof(SerialSummary));
            OnPropertyChanged(nameof(SerialDetail));
            OnPropertyChanged(nameof(IsSerialComplete));
        }

        partial void OnQuantityChanged(decimal value) => UpdateBaseQuantity();
        partial void OnSelectedUnitChanged(Unit? value) => UpdateBaseQuantity();

        // BaseQuantity = Quantity * hệ số đơn vị; service dùng giá trị này cho số dư kho và số serial
        private void UpdateBaseQuantity()
        {
            if (SelectedProduct != null && SelectedUnit != null)
            {
                BaseQuantity = _productUnitService.ConvertToBaseUnit(SelectedProduct.Id, SelectedUnit.Id, Quantity);
            }
            else
            {
                BaseQuantity = Quantity;
            }
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                Price = value.DefaultPrice;
                IsSerialRequired = value.IsSerialTracked;
                LoadUnits(value.Id);
                UpdateBaseQuantity();
            }
            else
            {
                AvailableUnits.Clear();
                IsSerialRequired = false;
                BaseQuantity = 0;
            }
        }

        private void LoadUnits(int productId)
        {
            var productUnits = _productUnitService.GetByProductId(productId, includeDefault: true);
            AvailableUnits.Clear();
            foreach (var pu in productUnits)
            {
                AvailableUnits.Add(pu.Unit);
            }

            if (SelectedProduct != null)
            {
                SelectedUnit = AvailableUnits.FirstOrDefault(u => u.Id == SelectedProduct.DefaultUnitId) 
                               ?? AvailableUnits.FirstOrDefault();
            }
        }
    }

    public partial class StockInViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockInService _stockInService;
        private readonly ProductUnitService _productUnitService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;
        private bool _reloadRequested;
        private readonly DebouncedAction _filterReload = new();

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Supplier> _availableSuppliers = new();
        [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();
        [ObservableProperty] private ObservableCollection<StockInLineEditor> _lines = new();

        [ObservableProperty] private int _stockInId;
        private byte[] _editingRowVersion = [];
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        [NotifyPropertyChangedFor(nameof(CanApprove))]
        private string _status = DocumentStatus.Draft;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        private bool _isPosted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        private bool _isViewMode;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private Warehouse? _selectedWarehouse;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private DateTime _importDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private bool _isWriting;
        [ObservableProperty] private string _writeStatus = string.Empty;

        // List view properties
        [ObservableProperty] private ObservableCollection<StockIn> _stockInList = new();
        [ObservableProperty] private bool _isListViewVisible = true;
        [ObservableProperty] private bool _isDetailViewVisible = false;
        [ObservableProperty] private string _searchDocumentCode = string.Empty;
        [ObservableProperty] private string _searchSupplierName = string.Empty;
        [ObservableProperty] private DateTime? _filterFromDate;
        [ObservableProperty] private DateTime? _filterToDate;

        // Advanced Filter
        [ObservableProperty] private bool _isAdvancedFilterOpen;
        [ObservableProperty] private Warehouse? _selectedWarehouseFilter;
        [ObservableProperty] private string _selectedStatusFilter = "Tất cả";
        public ObservableCollection<string> StatusOptions { get; } = new() { "Tất cả", "Phiếu nháp", "Đã ghi sổ" };

        partial void OnSearchDocumentCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSearchSupplierNameChanged(string value) => ScheduleFilterReload();
        partial void OnFilterFromDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnFilterToDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnSelectedWarehouseFilterChanged(Warehouse? value) => ScheduleFilterReload();
        partial void OnSelectedStatusFilterChanged(string value) => ScheduleFilterReload();

        // Footer stats
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _draftCount;
        [ObservableProperty] private int _postedCount;

        public decimal TotalAmount => Lines.Sum(l => l.Quantity * l.Price);
        public bool CanEdit => !IsPosted && !IsViewMode && StockDocumentUiLifecycle.IsDraft(Status);
        public bool CanApprove => StockDocumentUiLifecycle.IsDraft(Status)
            ? CanUserEdit
            : IsAdminOrManager && (StockDocumentUiLifecycle.IsPendingApproval(Status) || StockDocumentUiLifecycle.IsApproved(Status));
        public bool IsAdminOrManager => AuthorizationService.CanPerform(_currentUser, PermissionAction.ApproveStock);
        public bool CanUserEdit => _currentUser != null && (_currentUser.RoleCode == "Quản trị viên" || _currentUser.RoleCode == "Quản lý" || _currentUser.RoleCode == "Nhân viên kho");
        public Task InitializationTask { get; }

        private Task<bool> ExecuteWriteAsync(
            Func<CancellationToken, Task> write,
            CancellationToken cancellationToken) =>
            DatabaseWriteUi.ExecuteAsync(
                write,
                () => IsWriting,
                value => IsWriting = value,
                value => WriteStatus = value,
                LoadData,
                message => MessageBox.Show(message, "Lỗi"),
                cancellationToken);

        public StockInViewModel(AppUser currentUser, Func<AppDbContext>? contextFactory = null)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            _currentUser = currentUser;
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockInService = new StockInService(_contextFactory);
            _productUnitService = new ProductUnitService(_contextFactory);
            var refDataService = new ReferenceDataService(_contextFactory);

            Lines.CollectionChanged += (s, e) => 
            {
                if (e.NewItems != null)
                {
                    foreach (StockInLineEditor item in e.NewItems)
                    {
                        item.PropertyChanged += Line_PropertyChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (StockInLineEditor item in e.OldItems)
                    {
                        item.PropertyChanged -= Line_PropertyChanged;
                    }
                }
                OnPropertyChanged(nameof(TotalAmount));
            };

            InitializationTask = InitializeAsync(refDataService);
        }

        // nạp lookup trước rồi mới bật reload filter, tránh property khởi tạo tạo nhiều query
        private async Task InitializeAsync(ReferenceDataService refDataService)
        {
            try
            {
                var productsTask = Task.Run(() => _productService.GetAllProducts());
                var suppliersTask = Task.Run(() => refDataService.GetAllSuppliers());
                var warehousesTask = Task.Run(() => refDataService.GetAllWarehouses());

                await Task.WhenAll(productsTask, suppliersTask, warehousesTask);

                AvailableProducts = new ObservableCollection<Product>(await productsTask);
                AvailableSuppliers = new ObservableCollection<Supplier>(await suppliersTask);
                AvailableWarehouses = new ObservableCollection<Warehouse>(await warehousesTask);
                SelectedWarehouse = AvailableWarehouses.FirstOrDefault(warehouse => warehouse.IsDefault)
                    ?? AvailableWarehouses.FirstOrDefault();
                DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";
                await LoadDataAsync(true);
                _isInitialized = true;
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StockInLineEditor.Quantity) || e.PropertyName == nameof(StockInLineEditor.Price))
            {
                OnPropertyChanged(nameof(TotalAmount));
            }
        }

        private void ScheduleFilterReload()
        {
            if (_isInitialized)
            {
                _filterReload.Schedule(LoadData);
            }
        }

        private void LoadData()
        {
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            _ = LoadDataAsync(true);
        }

        // phân trang chụp filter/skip trước worker; reset xóa collection, load more nối thêm
        private async Task LoadDataAsync(bool reset)
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                if (reset)
                {
                    _skip = 0;
                    StockInList.Clear();
                }

                var code = SearchDocumentCode;
                var supplierName = SearchSupplierName;
                var fromDate = FilterFromDate;
                var toDate = FilterToDate;
                int? warehouseId = SelectedWarehouseFilter?.Id > 0 ? SelectedWarehouseFilter.Id : null;
                var status = SelectedStatusFilter;
                var skip = _skip;

                var dataTask = Task.Run(() => _stockInService.GetStockInPaged(
                    code, supplierName, fromDate, toDate, warehouseId, status, skip, PageSize));
                var statsTask = Task.Run(() => _stockInService.GetStockInStats(
                    code, supplierName, fromDate, toDate, warehouseId, status));

                await Task.WhenAll(dataTask, statsTask);
                var data = await dataTask;
                var stats = await statsTask;

                foreach (var item in data)
                {
                    StockInList.Add(item);
                }
                _skip += data.Count;

                TotalCount = stats.TotalCount;
                DraftCount = stats.DraftCount;
                PostedCount = stats.PostedCount;
            }
            catch (Exception)
            {
            }
            finally
            {
                _isLoading = false;
                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    LoadData();
                }
            }
        }

        [RelayCommand]
        private async Task LoadMore()
        {
            await LoadDataAsync(false);
        }

        [RelayCommand]
        private void ExportExcel()
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("PhieuNhapKho");
                
                // Headers
                var headers = new[] { "Mã Phiếu", "Ngày Nhập", "Nhà Cung Cấp", "Kho", "Người Lập", "Trạng Thái", "Ghi Chú", "Tổng Tiền" };
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cell(1, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
                }

                // Data
                for (int i = 0; i < StockInList.Count; i++)
                {
                    var si = StockInList[i];
                    decimal totalAmount = si.Lines.Sum(l => l.Quantity * l.UnitPrice);

                    worksheet.Cell(i + 2, 1).Value = si.DocumentCode;
                    worksheet.Cell(i + 2, 2).Value = si.ImportDate?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cell(i + 2, 3).Value = si.Supplier?.DisplayName ?? "";
                    worksheet.Cell(i + 2, 4).Value = si.Warehouse?.DisplayName ?? "";
                    worksheet.Cell(i + 2, 5).Value = si.Creator?.FullName ?? "";
                    worksheet.Cell(i + 2, 6).Value = (si.Status == DocumentStatus.Posted || si.Status == "đã ghi sổ") ? "Đã ghi sổ" : "Phiếu nháp";
                    worksheet.Cell(i + 2, 7).Value = si.Notes ?? "";
                    worksheet.Cell(i + 2, 8).Value = totalAmount;
                    worksheet.Cell(i + 2, 8).Style.NumberFormat.Format = "#,##0";
                }

                worksheet.Columns().AdjustToContents();

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"PhieuNhapKho_{DateTime.Now:yyyyMMdd_HHmm}"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    workbook.SaveAs(saveDialog.FileName);
                    MessageBox.Show("Xuất Excel thành công!", "Thông báo");
                }
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        [RelayCommand]
        private void ResetFilter()
        {
            SearchDocumentCode = string.Empty;
            SearchSupplierName = string.Empty;
            FilterFromDate = null;
            FilterToDate = null;
            SelectedWarehouseFilter = null;
            SelectedStatusFilter = "Tất cả";
            ScheduleFilterReload();
        }

        [RelayCommand]
        private void ToggleAdvancedFilter()
        {
            IsAdvancedFilterOpen = !IsAdvancedFilterOpen;
        }

        [RelayCommand]
        private void CreateNew()
        {
            ResetForm();
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void ViewDetail(StockIn si)
        {
            if (si == null) return;
            IsViewMode = true;
            LoadFromModel(si);
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void EditDetail(StockIn si)
        {
            if (si == null) return;
            if (!CanUserEdit)
            {
                MessageBox.Show("Bạn không có quyền chỉnh sửa phiếu này.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!StockDocumentUiLifecycle.IsDraft(si.Status))
            {
                MessageBox.Show("Chỉ có thể sửa phiếu nháp.", "Thông báo");
                return;
            }
            IsViewMode = false;
            LoadFromModel(si);
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void BackToList()
        {
            IsListViewVisible = true;
            IsDetailViewVisible = false;
            LoadData();
        }


        [RelayCommand]
        private void Print(StockIn? si)
        {
            if (si == null) return;
            try
            {
                var model = new DocumentPrintService(_contextFactory).LoadStockIn(si.Id);
                new Views.DocumentPrintWindow(model).ShowDialog();
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // chuyển snapshot chứng từ thành editor line và nạp serial đã lưu để view/edit cùng một nguồn
        private void LoadFromModel(StockIn si)
        {
            StockInId = si.Id;
            _editingRowVersion = si.RowVersion.ToArray();
            DocumentCode = si.DocumentCode;
            SelectedWarehouse = AvailableWarehouses.FirstOrDefault(w => w.Id == si.WarehouseId);
            SelectedSupplier = AvailableSuppliers.FirstOrDefault(s => s.Id == si.SupplierId);
            ImportDate = si.ImportDate ?? DateTime.Now;
            Notes = si.Notes ?? string.Empty;
            Status = si.Status;
            IsPosted = si.Status == DocumentStatus.Posted || si.Status == "đã ghi sổ";
            
            Lines.Clear();
            foreach (var line in si.Lines)
            {
                var editor = new StockInLineEditor(_productUnitService)
                {
                    Id = line.Id,
                    SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId) ?? line.Product,
                    Quantity = line.Quantity,
                    Price = line.UnitPrice,
                };
                editor.SelectedUnit = editor.AvailableUnits.FirstOrDefault(u => u.Id == line.UnitId) ?? line.Unit;

                var serialNumbers = StockDocumentUiLifecycle.IsDraft(si.Status)
                    && !string.IsNullOrWhiteSpace(line.DraftSerials)
                    ? line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : line.ProductSerials.Select(serial => serial.SerialNumber);
                foreach (var serialNumber in serialNumbers)
                {
                    editor.SerialNumbers.Add(serialNumber);
                }
                
                Lines.Add(editor);
            }
            
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(CanApprove));
        }

        [RelayCommand]
        private void AddLine()
        {
            if (!CanEdit) return;
            Lines.Add(new StockInLineEditor(_productUnitService));
        }

        [RelayCommand]
        private void RemoveLine(StockInLineEditor line)
        {
            if (!CanEdit) return;
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        // chỉ sản phẩm theo serial mới mở dialog; số serial phải khớp số lượng quy đổi trước khi post
        private void OpenSerialInput(StockInLineEditor line)
        {
            if (line == null || line.SelectedProduct == null) return;
            
            var existing = string.Join("\n", line.SerialNumbers);
            var isReadOnly = !CanEdit;
            var dialog = new Views.SerialInputWindow(existing, null, isReadOnly, requireNonEmptySerials: true);
            
            // Try to find the main window or active window to set as owner
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow;
            
            if (CanEdit)
            {
                if (dialog.ShowDialog() == true)
                {
                    var serials = StockInService.ParseSerialRange(dialog.SerialInput);
                    line.SerialNumbers.Clear();
                    foreach (var sn in serials)
                    {
                        line.SerialNumbers.Add(sn);
                    }
                    line.Quantity = serials.Count;
                    line.NotifySerialChanges();
                }
            }
            else
            {
                dialog.ShowDialog();
            }
        }

        [RelayCommand]
        // draft lưu nội dung để sửa tiếp, chưa thay số dư kho hoặc trạng thái serial
        private async Task SaveDraft(CancellationToken cancellationToken)
        {
            if (!ValidateForm()) return;

            var operationId = Guid.NewGuid();
            try
            {
                var si = CreateModel();
                var siLines = CreateLines();

                if (!await ExecuteWriteAsync(
                    async _ => await _stockInService.SaveDraftAsync(si, siLines, _currentUser.Id, operationId, cancellationToken),
                    cancellationToken)) return;
                StockInId = si.Id;
                _editingRowVersion = si.RowVersion.ToArray();
                Status = si.Status;
                
                MessageBox.Show("Đã lưu phiếu nháp thành công.", "Thông báo");
                BackToList();
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        [RelayCommand]
        // command chọn submit/approve/post theo quyền và trạng thái; service là nơi kiểm tra lại lifecycle trong transaction
        private async Task ConfirmAndPost(CancellationToken cancellationToken)
        {
            if (IsPosted || StockDocumentUiLifecycle.IsPosted(Status)) return;
            var isDraft = StockDocumentUiLifecycle.IsDraft(Status);
            if (isDraft && !CanUserEdit)
            {
                MessageBox.Show("Bạn không có quyền gửi duyệt phiếu.", "Thông báo");
                return;
            }
            if (!isDraft && (!IsAdminOrManager ||
                (!StockDocumentUiLifecycle.IsPendingApproval(Status) && !StockDocumentUiLifecycle.IsApproved(Status))))
            {
                MessageBox.Show("Bạn không có quyền duyệt và ghi sổ phiếu.", "Thông báo");
                return;
            }

            var operationId = Guid.NewGuid();
            if (isDraft)
            {
                if (!ValidateForm()) return;
                try
                {
                    var si = CreateModel();
                    var siLines = CreateLines();
                    if (!await ExecuteWriteAsync(
                        async _ => await _stockInService.SaveDraftAsync(si, siLines, _currentUser.Id, operationId, cancellationToken),
                        cancellationToken)) return;
                    StockInId = si.Id;
                    _editingRowVersion = si.RowVersion.ToArray();
                }
                catch (Exception)
                {
                    MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
                    return;
                }

                foreach (var line in Lines)
                {
                    if (line.IsSerialRequired && line.SerialNumbers.Count != (int)line.Quantity)
                    {
                        var result = MessageBox.Show(
                            $"Sản phẩm {line.SelectedProduct?.DisplayName} yêu cầu {(int)line.Quantity} serial, nhưng hiện mới có {line.SerialNumbers.Count}.\n\nBạn có muốn bổ sung serial trước khi gửi duyệt không?",
                            "Thiếu Serial", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.Yes) return;
                    }
                }
            }

            var confirmMessage = isDraft && !IsAdminOrManager
                ? "Bạn có chắc chắn muốn gửi duyệt phiếu nhập này? Sau khi gửi sẽ không thể chỉnh sửa."
                : "Bạn có chắc chắn muốn duyệt và ghi sổ phiếu nhập này? Sau khi gửi duyệt sẽ không thể chỉnh sửa.";
            var confirm = MessageBox.Show(confirmMessage, "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (StockDocumentUiLifecycle.IsDraft(Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ =>
                        {
                            _editingRowVersion = await _stockInService.SubmitForApprovalAsync(StockInId, _editingRowVersion, _currentUser.Id, operationId, cancellationToken);
                        },
                        cancellationToken)) return;
                    Status = DocumentStatus.PendingApproval;
                    if (!IsAdminOrManager)
                    {
                        MessageBox.Show("Đã gửi phiếu nhập kho chờ duyệt.", "Thông báo");
                        BackToList();
                        return;
                    }
                }
                if (StockDocumentUiLifecycle.IsPendingApproval(Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ =>
                        {
                            _editingRowVersion = await _stockInService.ApproveAsync(StockInId, _editingRowVersion, _currentUser.Id, operationId, cancellationToken);
                        },
                        cancellationToken)) return;
                    Status = DocumentStatus.Approved;
                }
                if (!await ExecuteWriteAsync(
                    async _ =>
                    {
                        _editingRowVersion = await _stockInService.PostAsync(StockInId, _editingRowVersion, _currentUser.Id, operationId, cancellationToken);
                    },
                    cancellationToken)) return;
                IsPosted = true;
                Status = DocumentStatus.Posted;

                MessageBox.Show("Đã ghi sổ thành công. Hàng hóa đã được nhập vào kho.", "Thông báo");
                ResetForm();
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        [RelayCommand]
        // approve dùng actor hiện tại và refresh lại danh sách sau khi service commit
        private async Task ApproveDocument(StockIn document, CancellationToken cancellationToken)
        {
            if (document == null) return;
            if (StockDocumentUiLifecycle.IsPosted(document.Status))
            {
                MessageBox.Show("Phiếu này đã được ghi sổ rồi.", "Thông báo");
                return;
            }
            if (!IsAdminOrManager)
            {
                MessageBox.Show("Bạn không có quyền duyệt phiếu.", "Thông báo");
                return;
            }
            if (!StockDocumentUiLifecycle.IsDraft(document.Status) &&
                !StockDocumentUiLifecycle.IsPendingApproval(document.Status) &&
                !StockDocumentUiLifecycle.IsApproved(document.Status))
            {
                MessageBox.Show("Trạng thái phiếu không cho phép duyệt hoặc ghi sổ.", "Thông báo");
                return;
            }

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn duyệt và ghi sổ phiếu nhập kho {document.DocumentCode} không?", "Xác nhận duyệt phiếu", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var operationId = Guid.NewGuid();
            try
            {
                if (StockDocumentUiLifecycle.IsDraft(document.Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ =>
                        {
                            document.RowVersion = await _stockInService.SubmitForApprovalAsync(document.Id, document.RowVersion, _currentUser.Id, operationId, cancellationToken);
                        },
                        cancellationToken)) return;
                    document.Status = DocumentStatus.PendingApproval;
                }
                if (StockDocumentUiLifecycle.IsPendingApproval(document.Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ =>
                        {
                            document.RowVersion = await _stockInService.ApproveAsync(document.Id, document.RowVersion, _currentUser.Id, operationId, cancellationToken);
                        },
                        cancellationToken)) return;
                    document.Status = DocumentStatus.Approved;
                }
                if (!await ExecuteWriteAsync(
                    async _ =>
                    {
                        document.RowVersion = await _stockInService.PostAsync(document.Id, document.RowVersion, _currentUser.Id, operationId, cancellationToken);
                    },
                    cancellationToken)) return;
                document.Status = DocumentStatus.Posted;
                MessageBox.Show("Duyệt và ghi sổ phiếu nhập kho thành công.", "Thông báo");
                LoadData();
            }
            catch (Exception)
            {
                LoadData();
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        // kiểm tra trường bắt buộc, số lượng dương và serial đủ trước khi gọi service
        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập đủ thông tin sản phẩm.", "Cảnh báo");
                return false;
            }

            if (SelectedWarehouse == null)
            {
                MessageBox.Show("Vui lòng chọn kho nhập.", "Cảnh báo");
                return false;
            }

            foreach (var line in Lines)
            {
                if (line.SelectedProduct == null)
                {
                    MessageBox.Show("Có dòng hàng chưa chọn sản phẩm. Vui lòng kiểm tra lại.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                if (line.SelectedUnit == null)
                {
                    MessageBox.Show($"Sản phẩm '{line.SelectedProduct.DisplayName}' chưa chọn đơn vị tính. Vui lòng kiểm tra lại.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        // tạo header mới từ state form, không mang navigation đang bind sang context service
        private StockIn CreateModel()
        {
            return new StockIn
            {
                Id = StockInId,
                DocumentCode = DocumentCode,
                WarehouseId = SelectedWarehouse?.Id ?? 0,
                SupplierId = SelectedSupplier?.Id,
                ImportDate = ImportDate,
                Notes = Notes,
                Status = DocumentStatus.Draft,
                CreatedBy = _currentUser.Id,
                CreatedAt = DateTime.Now,
                PurposeCode = "Purchase",
                RowVersion = _editingRowVersion.ToArray()
            };
        }

        // mỗi editor trở thành dòng service với unit, quantity, base quantity, giá và chuỗi serial nháp
        private List<StockInLine> CreateLines()
        {
            return Lines.Select(l => new StockInLine
            {
                ProductId = l.SelectedProduct?.Id ?? 0,
                UnitId = l.SelectedUnit?.Id ?? 0,
                Quantity = l.Quantity,
                BaseQuantity = _productUnitService.ConvertToBaseUnit(l.SelectedProduct?.Id ?? 0, l.SelectedUnit?.Id ?? 0, l.Quantity),
                UnitPrice = l.Price,
                ProductSerials = l.SerialNumbers.Select(sn => new ProductSerial 
                { 
                    SerialNumber = sn, 
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    CurrentWarehouseId = SelectedWarehouse?.Id,
                    CurrentStatus = "InStock"
                }).ToList()
            }).ToList();
        }

        [RelayCommand]
        private void Cancel()
        {
            ResetForm();
        }

        // reset id/status/view mode để lần tạo tiếp theo bắt đầu ở Draft
        private void ResetForm()
        {
            StockInId = 0;
            _editingRowVersion = [];
            Status = DocumentStatus.Draft;
            IsPosted = false;
            IsViewMode = false;
            OnPropertyChanged(nameof(CanEdit));
            
            Lines.Clear();
            DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";
        }

        public void RefreshData()
        {
            LoadData();
        }
    }
}
