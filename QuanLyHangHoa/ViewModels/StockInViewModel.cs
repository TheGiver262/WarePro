using System;
using System.Collections.Generic;
using QuanLyHangHoa.Data;
using System.Collections.ObjectModel;
using System.Linq;
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

        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSerialComplete))] private decimal _quantity = 1;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private Unit? _selectedUnit;
        [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
        [ObservableProperty] private ObservableCollection<string> _serialNumbers = new();
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSerialComplete))] private bool _isSerialRequired;
        [ObservableProperty] private decimal _baseQuantity;

        public string SerialSummary => SerialNumbers.Count > 0 ? $"{SerialNumbers.Count} Serial" : "Chưa có Serial";
        public bool IsSerialComplete => !IsSerialRequired || SerialNumbers.Count == (int)Quantity;

        public StockInLineEditor(ProductUnitService productUnitService)
        {
            _productUnitService = productUnitService;
            SerialNumbers.CollectionChanged += (s, e) => NotifySerialChanges();
        }

        public void NotifySerialChanges()
        {
            OnPropertyChanged(nameof(SerialSummary));
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

    public partial class StockInViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockInService _stockInService;
        private readonly ProductUnitService _productUnitService;
        private readonly AppUser _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Supplier> _availableSuppliers = new();
        [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();
        [ObservableProperty] private ObservableCollection<StockInLineEditor> _lines = new();

        [ObservableProperty] private int _stockInId;
        [ObservableProperty] private string _status = DocumentStatus.Draft;
        [ObservableProperty] private bool _isPosted;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private Warehouse? _selectedWarehouse;
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private DateTime _importDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;

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

        partial void OnSearchDocumentCodeChanged(string value) => LoadData();
        partial void OnSearchSupplierNameChanged(string value) => LoadData();
        partial void OnFilterFromDateChanged(DateTime? value) => LoadData();
        partial void OnFilterToDateChanged(DateTime? value) => LoadData();
        partial void OnSelectedWarehouseFilterChanged(Warehouse? value) => LoadData();
        partial void OnSelectedStatusFilterChanged(string value) => LoadData();

        // Footer stats
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _draftCount;
        [ObservableProperty] private int _postedCount;

        public decimal TotalAmount => Lines.Sum(l => l.Quantity * l.Price);
        public bool CanEdit => !IsPosted;
        public bool CanApprove => AuthorizationService.CanPerform(_currentUser, PermissionAction.ApproveStock) && (Status == DocumentStatus.Draft || Status == "nháp");

        public StockInViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1 };
            var factory = contextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext());
            _productService = new ProductService(factory);
            _stockInService = new StockInService(factory);
            _productUnitService = new ProductUnitService(factory);
            var refDataService = new ReferenceDataService(factory);

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableSuppliers = new ObservableCollection<Supplier>(refDataService.GetAllSuppliers());
            AvailableWarehouses = new ObservableCollection<Warehouse>(refDataService.GetAllWarehouses());
            
            SelectedWarehouse = AvailableWarehouses.FirstOrDefault(w => w.IsDefault) ?? AvailableWarehouses.FirstOrDefault();
            DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";

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
            
            LoadData();
        }

        private void Line_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StockInLineEditor.Quantity) || e.PropertyName == nameof(StockInLineEditor.Price))
            {
                OnPropertyChanged(nameof(TotalAmount));
            }
        }

        [RelayCommand]
        private void LoadData()
        {
            var all = _stockInService.GetAll();
            
            // Apply filters
            if (!string.IsNullOrWhiteSpace(SearchDocumentCode))
            {
                all = all.Where(s => s.DocumentCode.Contains(SearchDocumentCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            if (!string.IsNullOrWhiteSpace(SearchSupplierName))
            {
                all = all.Where(s => s.Supplier?.DisplayName.Contains(SearchSupplierName, StringComparison.OrdinalIgnoreCase) ?? false).ToList();
            }

            if (FilterFromDate.HasValue)
            {
                all = all.Where(s => s.ImportDate >= FilterFromDate.Value.Date).ToList();
            }

            if (FilterToDate.HasValue)
            {
                all = all.Where(s => s.ImportDate <= FilterToDate.Value.Date).ToList();
            }

            if (SelectedWarehouseFilter != null)
            {
                all = all.Where(s => s.WarehouseId == SelectedWarehouseFilter.Id).ToList();
            }

            if (SelectedStatusFilter != "Tất cả")
            {
                string targetStatus = SelectedStatusFilter == "Đã ghi sổ" ? DocumentStatus.Posted : DocumentStatus.Draft;
                all = all.Where(s => s.Status == targetStatus || (targetStatus == DocumentStatus.Draft && s.Status == "nháp") || (targetStatus == DocumentStatus.Posted && s.Status == "đã ghi sổ")).ToList();
            }

            StockInList = new ObservableCollection<StockIn>(all);
            TotalCount = all.Count;
            DraftCount = all.Count(s => s.Status == DocumentStatus.Draft || s.Status == "nháp");
            PostedCount = all.Count(s => s.Status == DocumentStatus.Posted || s.Status == "đã ghi sổ");
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
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi");
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
            LoadData();
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
            LoadFromModel(si);
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void EditDetail(StockIn si)
        {
            if (si == null) return;
            if (si.Status == DocumentStatus.Posted || si.Status == "đã ghi sổ")
            {
                MessageBox.Show("Không thể sửa phiếu đã ghi sổ.", "Thông báo");
                return;
            }
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
        private void Print(StockIn si)
        {
            MessageBox.Show($"Tính năng In phiếu {si?.DocumentCode} đang được phát triển.", "Thông báo");
        }

        private void LoadFromModel(StockIn si)
        {
            StockInId = si.Id;
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
                    SelectedProduct = AvailableProducts.FirstOrDefault(p => p.Id == line.ProductId) ?? line.Product,
                    Quantity = line.Quantity,
                    Price = line.UnitPrice,
                    SelectedUnit = line.Unit
                };
                
                foreach (var sn in line.ProductSerials)
                {
                    editor.SerialNumbers.Add(sn.SerialNumber);
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
        private void OpenSerialInput(StockInLineEditor line)
        {
            if (!CanEdit) return;
            if (line == null || line.SelectedProduct == null) return;
            
            var existing = string.Join("\n", line.SerialNumbers);
            var dialog = new Views.SerialInputWindow(existing);
            
            // Try to find the main window or active window to set as owner
            dialog.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow;
            
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

        [RelayCommand]
        private void SaveDraft()
        {
            if (!ValidateForm()) return;

            try
            {
                var si = CreateModel();
                var siLines = CreateLines();

                _stockInService.SaveDraft(si, siLines, _currentUser.Id);
                StockInId = si.Id;
                Status = si.Status;
                
                MessageBox.Show("Đã lưu phiếu nháp thành công.", "Thông báo");
                BackToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
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
                var si = CreateModel();
                var siLines = CreateLines();
                _stockInService.SaveDraft(si, siLines, _currentUser.Id);
                StockInId = si.Id;
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
            var confirm = MessageBox.Show("Bạn có chắc chắn muốn ghi sổ phiếu nhập này? Sau khi ghi sổ sẽ không thể chỉnh sửa.", "Xác nhận ghi sổ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _stockInService.Post(StockInId, _currentUser.Id);
                IsPosted = true;
                Status = DocumentStatus.Posted;
                OnPropertyChanged(nameof(CanEdit));
                
                MessageBox.Show("Đã ghi sổ thành công. Hàng hóa đã được nhập vào kho.", "Thông báo");
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

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
                PurposeCode = "Purchase"
            };
        }

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

        private void ResetForm()
        {
            StockInId = 0;
            Status = DocumentStatus.Draft;
            IsPosted = false;
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

