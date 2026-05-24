using System;
using QuanLyHangHoa.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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

        [ObservableProperty] private ObservableCollection<StockOut> _stockOutList = new();
        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<Customer> _availableCustomers;
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

        partial void OnSearchDocumentCodeChanged(string value) => LoadData();
        partial void OnSearchCustomerNameChanged(string value) => LoadData();
        partial void OnFilterFromDateChanged(DateTime? value) => LoadData();
        partial void OnFilterToDateChanged(DateTime? value) => LoadData();
        partial void OnSelectedWarehouseFilterChanged(Warehouse? value) => LoadData();
        partial void OnSelectedStatusFilterChanged(string value) => LoadData();
        
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

        public bool CanEdit => !IsPosted && !IsViewMode;
        public bool IsAdminOrManager => AuthorizationService.CanPerform(_currentUser, PermissionAction.ApproveStock);
        public bool CanApprove => IsAdminOrManager && (Status == DocumentStatus.Draft || Status == "nháp");
        public bool CanUserEdit => _currentUser != null && (_currentUser.RoleCode == "Quản trị viên" || _currentUser.RoleCode == "Quản lý" || _currentUser.RoleCode == "Nhân viên kho");

        public StockOutViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1, Username = "System", RoleCode = "Quản trị viên" }; // Default Admin role for safety
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockOutService = new StockOutService(_contextFactory);
            _customerService = new CustomerService(_contextFactory);
            _productUnitService = new ProductUnitService(_contextFactory);


            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableCustomers = new ObservableCollection<Customer>(_customerService.GetAll());
            
            using (var db = _contextFactory())
            {
                AvailableWarehouses = new ObservableCollection<Warehouse>(db.Warehouses.ToList());
            }
            
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
            LoadData();
        }

        private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StockOutLineEditor.Quantity) || e.PropertyName == nameof(StockOutLineEditor.Price))
            {
                RecalculateTotal();
            }
        }

        [RelayCommand]
        private void LoadData()
        {
            var data = _stockOutService.GetAll();
            
            if (!string.IsNullOrWhiteSpace(SearchDocumentCode))
            {
                data = data.Where(s => s.DocumentCode.Contains(SearchDocumentCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchCustomerName))
            {
                data = data.Where(s => s.Customer?.DisplayName.Contains(SearchCustomerName, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            }

            if (FilterFromDate.HasValue)
            {
                data = data.Where(s => s.ExportDate >= FilterFromDate.Value.Date).ToList();
            }

            if (FilterToDate.HasValue)
            {
                data = data.Where(s => s.ExportDate <= FilterToDate.Value.Date).ToList();
            }

            if (SelectedWarehouseFilter != null)
            {
                data = data.Where(s => s.WarehouseId == SelectedWarehouseFilter.Id).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SelectedStatusFilter) && SelectedStatusFilter != "Tất cả")
            {
                string dbStatus = SelectedStatusFilter == "Đã ghi sổ" ? DocumentStatus.Posted : DocumentStatus.Draft;
                data = data.Where(s => s.Status == dbStatus || (dbStatus == DocumentStatus.Draft && s.Status == "nháp") || (dbStatus == DocumentStatus.Posted && s.Status == "đã ghi sổ")).ToList();
            }

            StockOutList = new ObservableCollection<StockOut>(data);
            TotalCount = data.Count;
            DraftCount = data.Count(s => s.Status == DocumentStatus.Draft || s.Status == "nháp");
            PostedCount = data.Count(s => s.Status == DocumentStatus.Posted || s.Status == "đã ghi sổ");
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
            LoadData();
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
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi");
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
            if (stockOut.Status == DocumentStatus.Posted || stockOut.Status == "đã ghi sổ")
            {
                MessageBox.Show("Không thể sửa phiếu đã ghi sổ.", "Thông báo");
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
        private void OpenSerialInput(StockOutLineEditor line)
        {
            if (line == null || line.SelectedProduct == null) return;
            
            var isAdmin = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageUsers);
            var existing = string.Join("\n", line.SerialNumbers);
            var isReadOnly = !CanEdit && !isAdmin;

            List<ProductSerial> available;
            if (!CanEdit && isAdmin)
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

            var dialog = new Views.SerialInputWindow(existing, available, isReadOnly);
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
            else if (isAdmin)
            {
                if (dialog.ShowDialog() == true)
                {
                    var newSerials = StockInService.ParseSerialRange(dialog.SerialInput);
                    if (newSerials.Count != (int)line.Quantity)
                    {
                        MessageBox.Show($"Số lượng serial mới ({newSerials.Count}) phải khớp chính xác với số lượng của dòng hàng ({line.Quantity})!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    try
                    {
                        using (var db = _contextFactory())
                        {
                            var dbLine = db.StockOutLines
                                .Include(x => x.StockOut)
                                .Include(x => x.ProductSerials)
                                .FirstOrDefault(x => x.Id == line.Id);

                            if (dbLine == null)
                            {
                                MessageBox.Show("Không tìm thấy dòng phiếu xuất trong cơ sở dữ liệu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            var currentSerials = dbLine.ProductSerials.ToList();
                            var currentSnList = currentSerials.Select(s => s.SerialNumber).ToList();

                            var removedSnList = currentSnList.Except(newSerials, StringComparer.OrdinalIgnoreCase).ToList();
                            var addedSnList = newSerials.Except(currentSnList, StringComparer.OrdinalIgnoreCase).ToList();

                            // Validate các serial bị loại bỏ
                            foreach (var sn in removedSnList)
                            {
                                var ps = currentSerials.FirstOrDefault(x => x.SerialNumber.Equals(sn, StringComparison.OrdinalIgnoreCase));
                                if (ps != null)
                                {
                                    if (ps.CurrentStatus != SerialStatus.Sold.ToString())
                                    {
                                        MessageBox.Show($"Không thể thu hồi serial {sn} về kho vì trạng thái của nó đã thay đổi.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }
                                }
                            }

                            // Validate các serial mới được thêm
                            foreach (var sn in addedSnList)
                            {
                                var ps = db.ProductSerials.FirstOrDefault(x => x.SerialNumber == sn && x.ProductId == dbLine.ProductId);
                                if (ps == null)
                                {
                                    MessageBox.Show($"Không tìm thấy số serial {sn} của sản phẩm này trong hệ thống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                                if (ps.CurrentStatus != SerialStatus.InStock.ToString() || ps.CurrentWarehouseId != dbLine.StockOut.WarehouseId)
                                {
                                    MessageBox.Show($"Số serial {sn} không có sẵn (InStock) tại kho xuất của phiếu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            // Thực hiện cập nhật DB
                            // Với serial bị loại bỏ: trả lại kho nguồn, set status = InStock, LastStockOutLineId = null
                            foreach (var sn in removedSnList)
                            {
                                var ps = currentSerials.FirstOrDefault(x => x.SerialNumber.Equals(sn, StringComparison.OrdinalIgnoreCase));
                                if (ps != null)
                                {
                                    ps.CurrentStatus = SerialStatus.InStock.ToString();
                                    ps.CurrentWarehouseId = dbLine.StockOut.WarehouseId;
                                    ps.LastStockOutLineId = null;
                                }
                            }

                            // Với serial mới được thêm: set status = Sold, CurrentWarehouseId = null, LastStockOutLineId = line.Id
                            foreach (var sn in addedSnList)
                            {
                                var ps = db.ProductSerials.FirstOrDefault(x => x.SerialNumber == sn && x.ProductId == dbLine.ProductId);
                                if (ps != null)
                                {
                                    ps.CurrentStatus = SerialStatus.Sold.ToString();
                                    ps.CurrentWarehouseId = null;
                                    ps.LastStockOutLineId = dbLine.Id;
                                }
                            }

                            dbLine.DraftSerials = string.Join(",", newSerials);
                            db.SaveChanges();
                        }

                        // Đồng bộ UI
                        line.SerialNumbers.Clear();
                        foreach (var sn in newSerials)
                        {
                            line.SerialNumbers.Add(sn);
                        }
                        line.NotifySerialChanges();

                        MessageBox.Show("Cập nhật số serial thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Đã xảy ra lỗi khi lưu số serial: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
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
        private void SaveStockOut()
        {
            if (!ValidateForm()) return;

            try
            {
                var so = CreateModel();
                var soLines = CreateLines();

                _stockOutService.SaveDraft(so, soLines, _currentUser.Id);
                StockOutId = so.Id;
                Status = so.Status;
                
                MessageBox.Show("Đã lưu phiếu xuất kho nháp.", "Thông báo");
                BackToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi");
            }
        }

        [RelayCommand]
        private void ConfirmAndPost()
        {
            if (IsPosted) return;

            // 1. Always save current state as draft first to ensure DB is up to date
            if (!ValidateForm()) return;
            try
            {
                var so = CreateModel();
                var soLines = CreateLines();
                _stockOutService.SaveDraft(so, soLines, _currentUser.Id);
                StockOutId = so.Id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dữ liệu trước khi ghi sổ: {ex.Message}", "Lỗi");
                return;
            }

            // 2. Validate serials before posting
            foreach (var line in Lines)
            {
                if (line.IsSerialRequired && line.SerialNumbers.Count != (int)line.Quantity)
                {
                    var result = MessageBox.Show(
                        $"Sản phẩm {line.SelectedProduct?.DisplayName} yêu cầu { (int)line.Quantity } serial, nhưng hiện mới có {line.SerialNumbers.Count}.\n\nBạn có muốn bổ sung serial trước khi ghi sổ không?", 
                        "Thiếu Serial", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.Yes) return;
                }
            }

            // 3. Confirm posting
            var confirm = MessageBox.Show("Bạn có chắc chắn muốn ghi sổ phiếu xuất này? Sau khi ghi sổ sẽ không thể chỉnh sửa.", "Xác nhận ghi sổ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _stockOutService.Post(StockOutId, _currentUser.Id);
                IsPosted = true;
                Status = DocumentStatus.Posted;
                OnPropertyChanged(nameof(CanEdit));
                
                MessageBox.Show("Đã ghi sổ thành công. Hàng hóa đã được xuất khỏi kho.", "Thông báo");
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void ApproveDocument(StockOut document)
        {
            if (document == null) return;
            if (document.Status == DocumentStatus.Posted || document.Status == "đã ghi sổ")
            {
                MessageBox.Show("Phiếu này đã được ghi sổ rồi.", "Thông báo");
                return;
            }
            if (!IsAdminOrManager)
            {
                MessageBox.Show("Bạn không có quyền duyệt phiếu.", "Thông báo");
                return;
            }

            var confirm = MessageBox.Show($"Bạn có chắc chắn muốn duyệt và ghi sổ phiếu xuất kho {document.DocumentCode} không?", "Xác nhận duyệt phiếu", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _stockOutService.Post(document.Id, _currentUser.Id);
                MessageBox.Show("Duyệt và ghi sổ phiếu xuất kho thành công.", "Thông báo");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

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
        private void Print()
        {
            MessageBox.Show("Tính năng in đang được phát triển.", "Thông báo");
        }


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
