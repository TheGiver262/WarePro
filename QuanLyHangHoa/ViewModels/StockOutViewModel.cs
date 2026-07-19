using System;
using QuanLyHangHoa.Data;
using System.Collections.Generic;
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
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockOutLineEditor : ObservableObject
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

        public StockOutLineEditor(ProductUnitService productUnitService)
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

        // quy đổi số lượng theo unit đã chọn; serial count và tồn khả dụng đều so bằng base quantity
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

    public partial class StockOutViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockOutService _stockOutService;
        private readonly CustomerService _customerService;
        private readonly ProductUnitService _productUnitService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;
        private bool _reloadRequested;
        private readonly DebouncedAction _filterReload = new();

        [ObservableProperty] private ObservableCollection<StockOut> _stockOutList = new();
        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Customer> _availableCustomers = new();
        [ObservableProperty] private ObservableCollection<StockOutLineEditor> _lines = new();
        
        [ObservableProperty] private bool _isListViewVisible = true;
        [ObservableProperty] private bool _isDetailViewVisible = false;
        [ObservableProperty] private string _searchDocumentCode = string.Empty;
        [ObservableProperty] private string _searchCustomerName = string.Empty;
        [ObservableProperty] private DateTime? _filterFromDate;
        [ObservableProperty] private DateTime? _filterToDate;
        [ObservableProperty] private bool _isAdvancedFilterOpen;
        [ObservableProperty] private Warehouse? _selectedWarehouseFilter;
        [ObservableProperty] private string _selectedStatusFilter = "Tất cả";
        [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();
        public ObservableCollection<string> StatusOptions { get; } = new() { "Tất cả", "Phiếu nháp", "Đã ghi sổ" };

        partial void OnSearchDocumentCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSearchCustomerNameChanged(string value) => ScheduleFilterReload();
        partial void OnFilterFromDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnFilterToDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnSelectedWarehouseFilterChanged(Warehouse? value) => ScheduleFilterReload();
        partial void OnSelectedStatusFilterChanged(string value) => ScheduleFilterReload();
        
        // Footer Stats
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _draftCount;
        [ObservableProperty] private int _postedCount;

        [ObservableProperty] private int _stockOutId;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private DateTime _exportDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private Customer? _selectedCustomer;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanApprove))]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        private string _status = DocumentStatus.Draft;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        private bool _isPosted;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEdit))]
        private bool _isViewMode;

        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _isWriting;
        [ObservableProperty] private string _writeStatus = string.Empty;

        public bool CanEdit => !IsPosted && !IsViewMode && StockDocumentUiLifecycle.IsDraft(Status);
        public bool IsAdminOrManager => AuthorizationService.CanPerform(_currentUser, PermissionAction.ApproveStock);
        public bool CanApprove => StockDocumentUiLifecycle.IsDraft(Status)
            ? CanUserEdit
            : IsAdminOrManager && (StockDocumentUiLifecycle.IsPendingApproval(Status) || StockDocumentUiLifecycle.IsApproved(Status));
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

        public StockOutViewModel(AppUser currentUser, Func<AppDbContext>? contextFactory = null)
        {
            ArgumentNullException.ThrowIfNull(currentUser);
            _currentUser = currentUser;
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockOutService = new StockOutService(_contextFactory);
            _customerService = new CustomerService(_contextFactory);
            _productUnitService = new ProductUnitService(_contextFactory);
            
            Lines.CollectionChanged += (s, e) => 
            {
                if (e.NewItems != null)
                {
                    foreach (StockOutLineEditor item in e.NewItems)
                    {
                        item.PropertyChanged += Line_PropertyChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (StockOutLineEditor item in e.OldItems)
                    {
                        item.PropertyChanged -= Line_PropertyChanged;
                    }
                }
                RecalculateTotal();
            };

            InitializationTask = InitializeAsync();
        }

        // chờ lookup và dữ liệu đầu tiên xong trước khi đánh dấu ViewModel đã sẵn sàng
        private async Task InitializeAsync()
        {
            try
            {
                var productsTask = Task.Run(() => _productService.GetAllProducts());
                var customersTask = Task.Run(() => _customerService.GetAll());
                var warehousesTask = Task.Run(() =>
                {
                    using var db = _contextFactory();
                    return db.Warehouses.AsNoTracking().ToList();
                });

                await Task.WhenAll(productsTask, customersTask, warehousesTask);

                AvailableProducts = new ObservableCollection<Product>(await productsTask);
                AvailableCustomers = new ObservableCollection<Customer>(await customersTask);
                AvailableWarehouses = new ObservableCollection<Warehouse>(await warehousesTask);
                await LoadDataAsync(true);
                _isInitialized = true;
            }
            catch (Exception)
            {
                StatusMessage = DatabaseWriteUi.TechnicalErrorMessage;
            }
        }

        private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StockOutLineEditor.Quantity) || e.PropertyName == nameof(StockOutLineEditor.Price))
            {
                RecalculateTotal();
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

        // snapshot filter giúp trang dữ liệu và số thống kê dùng cùng điều kiện trong một lượt tải
        private async Task LoadDataAsync(bool reset)
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                if (reset)
                {
                    _skip = 0;
                    StockOutList.Clear();
                }

                var code = SearchDocumentCode;
                var customerName = SearchCustomerName;
                var fromDate = FilterFromDate;
                var toDate = FilterToDate;
                int? warehouseId = SelectedWarehouseFilter?.Id > 0 ? SelectedWarehouseFilter.Id : null;
                var status = SelectedStatusFilter;
                var skip = _skip;

                var dataTask = Task.Run(() => _stockOutService.GetStockOutPaged(
                    code, customerName, fromDate, toDate, warehouseId, status, skip, PageSize));
                var statsTask = Task.Run(() => _stockOutService.GetStockOutStats(
                    code, customerName, fromDate, toDate, warehouseId, status));

                await Task.WhenAll(dataTask, statsTask);
                var data = await dataTask;
                var stats = await statsTask;

                foreach (var item in data)
                {
                    StockOutList.Add(item);
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
        private void ResetFilter()
        {
            SearchDocumentCode = string.Empty;
            SearchCustomerName = string.Empty;
            FilterFromDate = null;
            FilterToDate = null;
            SelectedWarehouseFilter = null;
            SelectedStatusFilter = "Tất cả";
            ScheduleFilterReload();
        }

        [RelayCommand]
        private void ExportExcel()
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("PhieuXuatKho");
                
                // Headers
                var headers = new[] { "Mã Phiếu", "Ngày Xuất", "Khách Hàng", "Kho", "Người Lập", "Trạng Thái", "Ghi Chú", "Tổng Tiền" };
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cell(1, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGreen;
                }

                // Data
                for (int i = 0; i < StockOutList.Count; i++)
                {
                    var so = StockOutList[i];
                    decimal totalAmount = so.Lines.Sum(l => l.Quantity * l.UnitPrice);

                    worksheet.Cell(i + 2, 1).Value = so.DocumentCode;
                    worksheet.Cell(i + 2, 2).Value = so.ExportDate?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cell(i + 2, 3).Value = so.Customer?.DisplayName ?? "";
                    worksheet.Cell(i + 2, 4).Value = so.Warehouse?.DisplayName ?? "";
                    worksheet.Cell(i + 2, 5).Value = so.Creator?.FullName ?? "";
                    worksheet.Cell(i + 2, 6).Value = (so.Status == DocumentStatus.Posted || so.Status == "đã ghi sổ") ? "Đã ghi sổ" : "Phiếu nháp";
                    worksheet.Cell(i + 2, 7).Value = so.Notes ?? "";
                    worksheet.Cell(i + 2, 8).Value = totalAmount;
                    worksheet.Cell(i + 2, 8).Style.NumberFormat.Format = "#,##0";
                }

                worksheet.Columns().AdjustToContents();

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"PhieuXuatKho_{DateTime.Now:yyyyMMdd_HHmm}"
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
        private void ViewDetail(StockOut stockOut)
        {
            if (stockOut == null) return;
            IsViewMode = true;
            LoadToForm(stockOut);
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void EditDetail(StockOut stockOut)
        {
            if (stockOut == null) return;
            if (!CanUserEdit)
            {
                MessageBox.Show("Bạn không có quyền chỉnh sửa phiếu này.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!StockDocumentUiLifecycle.IsDraft(stockOut.Status))
            {
                MessageBox.Show("Chỉ có thể sửa phiếu nháp.", "Thông báo");
                return;
            }
            IsViewMode = false;
            LoadToForm(stockOut);
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

        // nạp header, line và serial từ database thành editor tách biệt cho view/edit
        private void LoadToForm(StockOut so)
        {
            StockOutId = so.Id;
            DocumentCode = so.DocumentCode;
            WarehouseId = so.WarehouseId;
            SelectedCustomer = AvailableCustomers.FirstOrDefault(c => c.Id == so.CustomerId);
            ExportDate = so.ExportDate ?? DateTime.Now;
            Notes = so.Notes ?? string.Empty;
            Status = so.Status;
            IsPosted = so.Status == DocumentStatus.Posted || so.Status == "đã ghi sổ";
            OnPropertyChanged(nameof(CanEdit));
            
            Lines.Clear();
            using (var db = _contextFactory())
            {
                foreach (var line in so.Lines)
                {
                    var editor = new StockOutLineEditor(_productUnitService)
                    {
                        Id = line.Id,
                        SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId),
                        Quantity = line.Quantity,
                        Price = line.UnitPrice,
                    };
                    editor.SelectedUnit = editor.AvailableUnits.FirstOrDefault(u => u.Id == line.UnitId) ?? db.Units.FirstOrDefault(u => u.Id == line.UnitId);

                    // Load serial numbers from DB
                    var serials = db.ProductSerials
                        .Where(ps => ps.LastStockOutLineId == line.Id)
                        .Select(ps => ps.SerialNumber)
                        .ToList();

                    foreach (var sn in serials)
                    {
                        editor.SerialNumbers.Add(sn);
                    }

                    Lines.Add(editor);
                }
            }
            RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalAmount = Lines.Sum(l => l.Quantity * l.Price);
        }

        [RelayCommand]
        // dialog chỉ cho chọn serial InStock đúng sản phẩm/kho; kết quả phải đủ theo quantity
        private void OpenSerialInput(StockOutLineEditor line)
        {
            if (line == null || line.SelectedProduct == null) return;
            
            var existing = string.Join("\n", line.SerialNumbers);
            var isReadOnly = !CanEdit;

            List<ProductSerial> available;
            if (!CanEdit)
            {
                using (var db = _contextFactory())
                {
                    available = db.ProductSerials
                        .Where(s => s.ProductId == line.SelectedProduct.Id && s.CurrentWarehouseId == WarehouseId && s.CurrentStatus == "InStock")
                        .ToList();
                    
                    var lineSerials = db.ProductSerials
                        .Where(s => s.ProductId == line.SelectedProduct.Id && s.LastStockOutLineId == line.Id)
                        .ToList();
                        
                    var existIds = new HashSet<int>(available.Select(a => a.Id));
                    foreach (var ls in lineSerials)
                    {
                        if (!existIds.Contains(ls.Id))
                        {
                            available.Add(ls);
                        }
                    }
                }
            }
            else
            {
                available = _stockOutService.GetInStockSerials(line.SelectedProduct.Id, WarehouseId);
            }

            var dialog = new Views.SerialInputWindow(existing, available, isReadOnly, requireNonEmptySerials: true);
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow;
            
            if (CanEdit)
            {
                if (dialog.ShowDialog() == true)
                {
                    var serials = StockInService.ParseSerialRange(dialog.SerialInput);
                    var availableSerialsSet = new HashSet<string>(available.Select(s => s.SerialNumber), StringComparer.OrdinalIgnoreCase);
                    var invalidSerials = serials.Where(s => !availableSerialsSet.Contains(s)).ToList();

                    if (invalidSerials.Any())
                    {
                        MessageBox.Show(
                            $"Các serial sau không có sẵn trong kho hoặc không thuộc sản phẩm này: {string.Join(", ", invalidSerials)}.\n\nVui lòng chỉ chọn từ danh sách serial hiện có.",
                            "Lỗi Serial", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

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
        private void AddLine()
        {
            Lines.Add(new StockOutLineEditor(_productUnitService));
        }

        [RelayCommand]
        private void RemoveLine(StockOutLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        // chặn dòng thiếu sản phẩm, quantity không hợp lệ và serial không đủ trước khi gọi service
        private bool ValidateForm()
        {
            if (SelectedCustomer == null || !Lines.Any())
            {
                MessageBox.Show("Vui lòng chọn khách hàng và nhập ít nhất 1 mặt hàng.", "Thông báo");
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
                if (line.IsSerialRequired && line.SerialNumbers.Count != (int)line.Quantity)
                {
                    MessageBox.Show($"Sản phẩm {line.SelectedProduct.DisplayName} yêu cầu {(int)line.Quantity} serial, nhưng hiện có {line.SerialNumbers.Count}.", "Thiếu Serial", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        [RelayCommand]
        // lưu draft không trừ tồn; model header và line được dựng mới từ form
        private async Task SaveStockOut(CancellationToken cancellationToken)
        {
            var operationId = Guid.NewGuid();
            if (!ValidateForm()) return;

            try
            {
                var so = CreateModel();
                var soLines = CreateLines();

                if (!await ExecuteWriteAsync(
                    async _ => await _stockOutService.SaveDraftAsync(so, soLines, _currentUser.Id, operationId, cancellationToken),
                    cancellationToken)) return;
                StockOutId = so.Id;
                Status = so.Status;
                
                MessageBox.Show("Đã lưu phiếu xuất kho nháp.", "Thông báo");
                BackToList();
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        [RelayCommand]
        // quyền/lifecycle quyết định submit, approve hay post; service kiểm tra tồn lần cuối và commit atomic
        private async Task ConfirmAndPost(CancellationToken cancellationToken)
        {
            var operationId = Guid.NewGuid();
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

            if (isDraft)
            {
                if (!ValidateForm()) return;
                try
                {
                    var so = CreateModel();
                    var soLines = CreateLines();
                    if (!await ExecuteWriteAsync(
                        async _ => await _stockOutService.SaveDraftAsync(so, soLines, _currentUser.Id, operationId, cancellationToken),
                        cancellationToken)) return;
                    StockOutId = so.Id;
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
                ? "Bạn có chắc chắn muốn gửi duyệt phiếu xuất này? Sau khi gửi sẽ không thể chỉnh sửa."
                : "Bạn có chắc chắn muốn duyệt và ghi sổ phiếu xuất này? Sau khi gửi duyệt sẽ không thể chỉnh sửa.";
            var confirm = MessageBox.Show(confirmMessage, "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (StockDocumentUiLifecycle.IsDraft(Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ => await _stockOutService.SubmitForApprovalAsync(StockOutId, _currentUser.Id, operationId, cancellationToken),
                        cancellationToken)) return;
                    Status = DocumentStatus.PendingApproval;
                    if (!IsAdminOrManager)
                    {
                        MessageBox.Show("Đã gửi phiếu xuất kho chờ duyệt.", "Thông báo");
                        BackToList();
                        return;
                    }
                }
                if (StockDocumentUiLifecycle.IsPendingApproval(Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ => await _stockOutService.ApproveAsync(StockOutId, _currentUser.Id, operationId, cancellationToken),
                        cancellationToken)) return;
                    Status = DocumentStatus.Approved;
                }
                if (!await ExecuteWriteAsync(
                    async _ => await _stockOutService.PostAsync(StockOutId, _currentUser.Id, operationId, cancellationToken),
                    cancellationToken)) return;
                IsPosted = true;
                Status = DocumentStatus.Posted;

                MessageBox.Show("Đã ghi sổ thành công. Hàng hóa đã được xuất khỏi kho.", "Thông báo");
                ResetForm();
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        [RelayCommand]
        // không dùng object đang bind để ghi trực tiếp; chuyển id và actor cho service xử lý
        private async Task ApproveDocument(StockOut document, CancellationToken cancellationToken)
        {
            var operationId = Guid.NewGuid();
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

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn duyệt và ghi sổ phiếu xuất kho {document.DocumentCode} không?", "Xác nhận duyệt phiếu", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                if (StockDocumentUiLifecycle.IsDraft(document.Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ => await _stockOutService.SubmitForApprovalAsync(document.Id, _currentUser.Id, operationId, cancellationToken),
                        cancellationToken)) return;
                    document.Status = DocumentStatus.PendingApproval;
                }
                if (StockDocumentUiLifecycle.IsPendingApproval(document.Status))
                {
                    if (!await ExecuteWriteAsync(
                        async _ => await _stockOutService.ApproveAsync(document.Id, _currentUser.Id, operationId, cancellationToken),
                        cancellationToken)) return;
                    document.Status = DocumentStatus.Approved;
                }
                if (!await ExecuteWriteAsync(
                    async _ => await _stockOutService.PostAsync(document.Id, _currentUser.Id, operationId, cancellationToken),
                    cancellationToken)) return;
                document.Status = DocumentStatus.Posted;
                MessageBox.Show("Duyệt và ghi sổ phiếu xuất kho thành công.", "Thông báo");
                LoadData();
            }
            catch (Exception)
            {
                LoadData();
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi");
            }
        }

        // header chỉ chứa khóa ngoại và field nghiệp vụ, tránh attach navigation UI
        private StockOut CreateModel()
        {
            return new StockOut
            {
                Id = StockOutId,
                DocumentCode = DocumentCode,
                WarehouseId = WarehouseId,
                CustomerId = SelectedCustomer?.Id ?? 0,
                ExportDate = ExportDate,
                Notes = Notes,
                Status = DocumentStatus.Draft,
                CreatedBy = _currentUser.Id,
                CreatedAt = DateTime.Now,
                PurposeCode = "Sale"
            };
        }

        // map editor sang line gồm cả BaseQuantity và DraftSerials để posting service đối soát
        private List<StockOutLine> CreateLines()
        {
            return Lines.Select(l => {
                var line = new StockOutLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    Quantity = l.Quantity,
                    UnitPrice = l.Price,
                    UnitId = l.SelectedUnit?.Id ?? 0,
                    BaseQuantity = _productUnitService.ConvertToBaseUnit(l.SelectedProduct?.Id ?? 0, l.SelectedUnit?.Id ?? 0, l.Quantity)
                };
                
                if (l.IsSerialRequired)
                {
                    foreach (var sn in l.SerialNumbers)
                    {
                        line.ProductSerials.Add(new ProductSerial 
                        { 
                            SerialNumber = sn,
                            ProductId = line.ProductId
                        });
                    }
                }
                return line;
            }).ToList();
        }

        [RelayCommand]
        // bản in được tải lại đầy đủ thay vì dùng row phân trang thiếu navigation
        private void Print(StockOut? stockOut)
        {
            if (stockOut == null) return;
            try
            {
                var model = new DocumentPrintService(_contextFactory).LoadStockOut(stockOut.Id);
                new Views.DocumentPrintWindow(model).ShowDialog();
            }
            catch (Exception)
            {
                MessageBox.Show(DatabaseWriteUi.TechnicalErrorMessage, "Lỗi",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // đưa form về Draft, bỏ id và serial của chứng từ trước
        private void ResetForm()
        {
            StockOutId = 0;
            Lines.Clear();
            DocumentCode = $"OUT-{DateTime.Now:yyyyMMddHHmmss}";
            Notes = string.Empty;
            SelectedCustomer = null;
            ExportDate = DateTime.Now;
            TotalAmount = 0;
            Status = DocumentStatus.Draft;
            IsPosted = false;
            IsViewMode = false;
            OnPropertyChanged(nameof(CanEdit));
        }

        public void RefreshData()
        {
            LoadData();
        }
    }
}
