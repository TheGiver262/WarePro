using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockAdjustmentLineEditor : ObservableObject
    {
        private readonly ProductUnitService _unitService;
        private readonly ProductSerialService _serialService;

        public StockAdjustmentLineEditor(ProductUnitService unitService, ProductSerialService serialService)
        {
            _unitService = unitService;
            _serialService = serialService;
        }

        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private string _direction = "In";
        [ObservableProperty] private decimal _quantity = 1;
        [ObservableProperty] private decimal _baseQuantity;
        [ObservableProperty] private ProductUnit? _selectedUnit;
        [ObservableProperty] private ObservableCollection<ProductUnit> _availableUnits = new();
        [ObservableProperty] private ProductSerial? _selectedSerial;
        [ObservableProperty] private ObservableCollection<ProductSerial> _availableSerials = new();
        [ObservableProperty] private bool _isSerialTracked;
        [ObservableProperty] private string _serialNumbers = string.Empty;

        public string SerialDisplay => SerialNumbers;

        partial void OnSelectedProductChanged(Product? value)
        {
            AvailableUnits.Clear();
            AvailableSerials.Clear();
            IsSerialTracked = value?.IsSerialTracked ?? false;
            SerialNumbers = string.Empty;
            SelectedSerial = null;

            if (value != null)
            {
                var units = _unitService.GetByProductId(value.Id);
                foreach (var unit in units) AvailableUnits.Add(unit);
                SelectedUnit = AvailableUnits.FirstOrDefault(u => u.IsBaseUnit) ?? AvailableUnits.FirstOrDefault();
                
                if (IsSerialTracked)
                {
                    // Fetch serials for the product that are in stock
                    var serials = _serialService.SearchSerials(string.Empty, value.ProductCode, string.Empty, "InStock");
                    foreach (var serial in serials) AvailableSerials.Add(serial);
                }
            }
        }

        partial void OnSelectedUnitChanged(ProductUnit? value)
        {
            UpdateBaseQuantity();
        }

        partial void OnQuantityChanged(decimal value)
        {
            UpdateBaseQuantity();
        }

        partial void OnSerialNumbersChanged(string value)
        {
            OnPropertyChanged(nameof(SerialDisplay));
        }

        private void UpdateBaseQuantity()
        {
            BaseQuantity = Quantity * (SelectedUnit?.ConversionFactor ?? 1);
        }
    }

    public partial class StockAdjustmentViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockAdjustmentService _adjustmentService;
        private readonly ReferenceDataService _referenceDataService;
        private readonly ProductUnitService _unitService;
        private readonly ProductSerialService _serialService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;

        // View State
        [ObservableProperty] private bool _isListViewVisible = true;
        [ObservableProperty] private bool _isDetailViewVisible = false;
        [ObservableProperty] private bool _isEditMode = false;

        // List Data
        [ObservableProperty] private ObservableCollection<StockAdjustment> _adjustmentList = new();
        [ObservableProperty] private string _searchDocumentCode = string.Empty;
        [ObservableProperty] private DateTime? _filterFromDate;
        [ObservableProperty] private DateTime? _filterToDate;
        
        // Summary
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _draftCount;
        [ObservableProperty] private int _postedCount;

        // Detail Data
        [ObservableProperty] private int _editingId;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private int _warehouseId;
        [ObservableProperty] private string _adjustmentType = "Manual";
        [ObservableProperty] private string _reasonCode = "DAMAGED";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanProcessLifecycle))]
        private string _status = DocumentStatus.Draft;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private ObservableCollection<StockAdjustmentLineEditor> _lines = new();

        // Lookups
        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();
        [ObservableProperty] private List<string> _availableReasons = new() { "DAMAGED", "EXPIRED", "INVENTORY_COUNT", "LOST", "OTHER" };

        public bool IsAdminOrManager => AuthorizationService.CanPerform(_currentUser, PermissionAction.ApproveStock);
        public bool CanUserEdit => _currentUser.RoleCode == "Quản trị viên" ||
            _currentUser.RoleCode == "Quản lý" || _currentUser.RoleCode == "Nhân viên kho";
        public bool CanProcessLifecycle => StockDocumentUiLifecycle.IsDraft(Status)
            ? CanUserEdit
            : IsAdminOrManager && (StockDocumentUiLifecycle.IsPendingApproval(Status) || StockDocumentUiLifecycle.IsApproved(Status));

        partial void OnStatusChanged(string value)
        {
            if (!StockDocumentUiLifecycle.IsDraft(value))
            {
                IsEditMode = false;
            }
        }

        public StockAdjustmentViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1, Username = "System", RoleCode = "Quản trị viên" };
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _adjustmentService = new StockAdjustmentService(_contextFactory);
            _referenceDataService = new ReferenceDataService(_contextFactory);
            _unitService = new ProductUnitService(_contextFactory);
            _serialService = new ProductSerialService(_contextFactory);

            LoadInitialData();
            LoadData();
        }

        private void LoadInitialData()
        {
            // Load products with serials to support adjustment
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableWarehouses = new ObservableCollection<Warehouse>(_referenceDataService.GetAllWarehouses());
            WarehouseId = AvailableWarehouses.FirstOrDefault(w => w.IsDefault)?.Id ?? AvailableWarehouses.FirstOrDefault()?.Id ?? 1;
        }

        [RelayCommand]
        private void LoadData()
        {
            var all = _adjustmentService.GetAll();
            
            var filtered = all.AsQueryable();
            if (!string.IsNullOrWhiteSpace(SearchDocumentCode))
                filtered = filtered.Where(x => x.DocumentCode.Contains(SearchDocumentCode, StringComparison.OrdinalIgnoreCase));
            
            if (FilterFromDate.HasValue)
                filtered = filtered.Where(x => (x.PostedAt ?? x.ApprovedAt ?? DateTime.MinValue).Date >= FilterFromDate.Value.Date);
            
            if (FilterToDate.HasValue)
                filtered = filtered.Where(x => (x.PostedAt ?? x.ApprovedAt ?? DateTime.MaxValue).Date <= FilterToDate.Value.Date);

            AdjustmentList.Clear();
            foreach (var item in filtered) AdjustmentList.Add(item);
            UpdateSummaries();
        }

        private void UpdateSummaries()
        {
            TotalCount = AdjustmentList.Count;
            DraftCount = AdjustmentList.Count(x => x.Status == DocumentStatus.Draft || x.Status == "nháp");
            PostedCount = AdjustmentList.Count(x => x.Status == DocumentStatus.Posted || x.Status == "đã ghi sổ");
        }

        [RelayCommand]
        private void CreateNew()
        {
            EditingId = 0;
            DocumentCode = $"ADJ-{DateTime.Now:yyyyMMddHHmmss}";
            WarehouseId = AvailableWarehouses.FirstOrDefault(w => w.IsDefault)?.Id ?? AvailableWarehouses.FirstOrDefault()?.Id ?? 1;
            AdjustmentType = "Manual";
            ReasonCode = "DAMAGED";
            Status = DocumentStatus.Draft;
            Notes = string.Empty;
            Lines.Clear();

            IsListViewVisible = false;
            IsDetailViewVisible = true;
            IsEditMode = true;
        }

        [RelayCommand]
        private void ViewDetail(StockAdjustment item)
        {
            if (item == null) return;
            LoadForEditing(item.Id, false);
        }

        [RelayCommand]
        private void EditDetail(StockAdjustment item)
        {
            if (item == null) return;
            if (!CanUserEdit)
            {
                MessageBox.Show("Bạn không có quyền chỉnh sửa phiếu này.", "Thông báo");
                return;
            }
            if (!StockDocumentUiLifecycle.IsDraft(item.Status))
            {
                MessageBox.Show("Chỉ có thể sửa phiếu nháp.", "Thông báo");
                return;
            }
            LoadForEditing(item.Id, true);
        }

        private void LoadForEditing(int id, bool editMode)
        {
            var adj = _adjustmentService.GetById(id);
            if (adj == null) return;

            EditingId = adj.Id;
            DocumentCode = adj.DocumentCode;
            WarehouseId = adj.WarehouseId;
            AdjustmentType = adj.AdjustmentType;
            ReasonCode = adj.ReasonCode;
            Status = adj.Status;
            Notes = adj.Notes ?? string.Empty;

            Lines.Clear();
            foreach (var line in adj.Lines)
            {
                var editor = new StockAdjustmentLineEditor(_unitService, _serialService)
                {
                    SelectedProduct = line.Product,
                    Direction = line.Direction,
                    Quantity = line.QuantityDelta,
                    BaseQuantity = line.BaseQuantityDelta,
                    SelectedSerial = line.ProductSerial,
                    SerialNumbers = line.DraftSerials ?? line.ProductSerial?.SerialNumber ?? string.Empty
                };
                Lines.Add(editor);
            }

            IsListViewVisible = false;
            IsDetailViewVisible = true;
            IsEditMode = editMode && StockDocumentUiLifecycle.IsDraft(Status);
        }

        [RelayCommand]
        private void BackToList()
        {
            IsListViewVisible = true;
            IsDetailViewVisible = false;
            LoadData();
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockAdjustmentLineEditor(_unitService, _serialService));
        }

        [RelayCommand]
        private void RemoveLine(StockAdjustmentLineEditor line)
        {
            if (line != null) Lines.Remove(line);
        }

        [RelayCommand]
        private void OpenSerialWindow(StockAdjustmentLineEditor? line)
        {
            if (line?.SelectedProduct == null || !line.IsSerialTracked || !IsEditMode)
            {
                return;
            }

            if (line.BaseQuantity <= 0 || line.BaseQuantity != decimal.Truncate(line.BaseQuantity))
            {
                MessageBox.Show("Số lượng cơ sở của sản phẩm theo dõi serial phải là số nguyên dương.", "Cảnh báo");
                return;
            }

            List<ProductSerial>? availableSerials = null;
            if (line.Direction == "Out")
            {
                using var db = _contextFactory();
                availableSerials = db.ProductSerials
                    .AsNoTracking()
                    .Where(item => item.ProductId == line.SelectedProduct.Id &&
                                   item.CurrentWarehouseId == WarehouseId &&
                                   item.CurrentStatus == "InStock")
                    .OrderBy(item => item.SerialNumber)
                    .ToList();
            }

            var dialog = new SerialInputWindow(line.SerialNumbers, availableSerials, false);
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var serialNumbers = StockInService.ParseSerialRange(dialog.SerialInput);
            var expectedCount = (int)line.BaseQuantity;
            if (serialNumbers.Count != expectedCount)
            {
                MessageBox.Show(
                    $"Sản phẩm {line.SelectedProduct.DisplayName} yêu cầu {expectedCount} serial, nhưng hiện có {serialNumbers.Count}.",
                    "Cảnh báo");
                return;
            }

            if (serialNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serialNumbers.Count)
            {
                MessageBox.Show("Danh sách serial không được trùng lặp.", "Cảnh báo");
                return;
            }

            line.SerialNumbers = string.Join(",", serialNumbers);
            line.SelectedSerial = null;
        }

        [RelayCommand]
        private void SaveDraft()
        {
            if (!Validate()) return;

            try
            {
                var adj = new StockAdjustment
                {
                    Id = EditingId,
                    DocumentCode = DocumentCode,
                    WarehouseId = WarehouseId,
                    AdjustmentType = AdjustmentType,
                    ReasonCode = ReasonCode,
                    Notes = Notes,
                    Status = Status
                };

                var lineModels = Lines.Select(l => new StockAdjustmentLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    QuantityDelta = l.Quantity,
                    BaseQuantityDelta = l.BaseQuantity,
                    Direction = l.Direction,
                    ProductSerialId = string.IsNullOrWhiteSpace(l.SerialNumbers) ? l.SelectedSerial?.Id : null,
                    DraftSerials = string.IsNullOrWhiteSpace(l.SerialNumbers) ? null : l.SerialNumbers
                }).ToList();

                _adjustmentService.SaveDraft(adj, lineModels, _currentUser.Id);
                MessageBox.Show("Đã lưu bản nháp.", "Thông báo");
                EditingId = adj.Id;
                Status = adj.Status;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void ConfirmAndPost()
        {
            if (StockDocumentUiLifecycle.IsPosted(Status)) return;
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

            if (isDraft && !Validate()) return;

            var confirmMessage = isDraft && !IsAdminOrManager
                ? "Bạn có chắc chắn muốn gửi duyệt phiếu điều chỉnh này? Sau khi gửi sẽ không thể chỉnh sửa."
                : "Bạn có chắc chắn muốn duyệt và ghi sổ phiếu điều chỉnh này? Sau khi gửi duyệt sẽ không thể chỉnh sửa.";
            var result = MessageBox.Show(confirmMessage, "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (StockDocumentUiLifecycle.IsDraft(Status))
                {
                    var adjustment = new StockAdjustment
                    {
                        Id = EditingId,
                        DocumentCode = DocumentCode,
                        WarehouseId = WarehouseId,
                        AdjustmentType = AdjustmentType,
                        ReasonCode = ReasonCode,
                        Notes = Notes,
                        Status = Status
                    };
                    var lineModels = Lines.Select(line => new StockAdjustmentLine
                    {
                        ProductId = line.SelectedProduct?.Id ?? 0,
                        QuantityDelta = line.Quantity,
                        BaseQuantityDelta = line.BaseQuantity,
                        Direction = line.Direction,
                        ProductSerialId = string.IsNullOrWhiteSpace(line.SerialNumbers) ? line.SelectedSerial?.Id : null,
                        DraftSerials = string.IsNullOrWhiteSpace(line.SerialNumbers) ? null : line.SerialNumbers
                    }).ToList();
                    _adjustmentService.SaveDraft(adjustment, lineModels, _currentUser.Id);
                    EditingId = adjustment.Id;
                    Status = adjustment.Status;
                    _adjustmentService.SubmitForApproval(EditingId, _currentUser.Id);
                    Status = DocumentStatus.PendingApproval;
                    if (!IsAdminOrManager)
                    {
                        MessageBox.Show("Đã gửi phiếu điều chỉnh chờ duyệt.", "Thông báo");
                        BackToList();
                        return;
                    }
                }
                if (StockDocumentUiLifecycle.IsPendingApproval(Status))
                {
                    _adjustmentService.Approve(EditingId, _currentUser.Id);
                    Status = DocumentStatus.Approved;
                }
                _adjustmentService.Post(EditingId, _currentUser.Id);
                Status = DocumentStatus.Posted;
                MessageBox.Show("Đã ghi sổ thành công.", "Thông báo");
                BackToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void ExportExcel()
        {
            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook();
                var worksheet = workbook.Worksheets.Add("PhieuDieuChinh");
                
                // Headers
                var headers = new[] { "Mã Phiếu", "Ngày Điều Chỉnh", "Kho", "Lý Do", "Trạng Thái", "Ghi Chú" };
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cell(1, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
                }

                // Data
                for (int i = 0; i < AdjustmentList.Count; i++)
                {
                    var adj = AdjustmentList[i];

                    worksheet.Cell(i + 2, 1).Value = adj.DocumentCode;
                    worksheet.Cell(i + 2, 2).Value = adj.PostedAt?.ToString("dd/MM/yyyy HH:mm") ?? adj.ApprovedAt?.ToString("dd/MM/yyyy HH:mm") ?? "";
                    worksheet.Cell(i + 2, 3).Value = adj.Warehouse?.DisplayName ?? "";
                    worksheet.Cell(i + 2, 4).Value = adj.ReasonCode;
                    worksheet.Cell(i + 2, 5).Value = (adj.Status == DocumentStatus.Posted || adj.Status == "đã ghi sổ") ? "Đã ghi sổ" : "Phiếu nháp";
                    worksheet.Cell(i + 2, 6).Value = adj.Notes ?? "";
                }

                worksheet.Columns().AdjustToContents();

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = $"PhieuDieuChinh_{DateTime.Now:yyyyMMdd_HHmm}"
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
            FilterFromDate = null;
            FilterToDate = null;
            LoadData();
        }

        private bool Validate()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode))
            {
                MessageBox.Show("Vui lòng nhập mã chứng từ.", "Cảnh báo");
                return false;
            }

            if (!Lines.Any())
            {
                MessageBox.Show("Vui lòng thêm ít nhất một dòng hàng.", "Cảnh báo");
                return false;
            }

            foreach (var line in Lines)
            {
                if (line.SelectedProduct == null)
                {
                    MessageBox.Show("Vui lòng chọn sản phẩm cho tất cả các dòng.", "Cảnh báo");
                    return false;
                }
                if (line.Quantity <= 0)
                {
                    MessageBox.Show($"Số lượng của sản phẩm {line.SelectedProduct.DisplayName} phải lớn hơn 0.", "Cảnh báo");
                    return false;
                }
                if (line.IsSerialTracked)
                {
                    if (line.BaseQuantity != decimal.Truncate(line.BaseQuantity))
                    {
                        MessageBox.Show($"Số lượng cơ sở của sản phẩm {line.SelectedProduct.DisplayName} phải là số nguyên.", "Cảnh báo");
                        return false;
                    }

                    var serialNumbers = StockInService.ParseSerialRange(line.SerialNumbers);
                    var requiredCount = (int)line.BaseQuantity;
                    if (serialNumbers.Count != requiredCount)
                    {
                        MessageBox.Show($"Sản phẩm {line.SelectedProduct.DisplayName} yêu cầu {requiredCount} serial, nhưng hiện có {serialNumbers.Count}.", "Cảnh báo");
                        return false;
                    }

                    if (serialNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != serialNumbers.Count)
                    {
                        MessageBox.Show($"Serial của sản phẩm {line.SelectedProduct.DisplayName} không được trùng lặp.", "Cảnh báo");
                        return false;
                    }

                    using var db = _contextFactory();
                    if (line.Direction == "In")
                    {
                        var existingSerials = db.ProductSerials
                            .Where(item => serialNumbers.Contains(item.SerialNumber))
                            .Select(item => item.SerialNumber)
                            .ToList();
                        if (existingSerials.Count > 0)
                        {
                            MessageBox.Show($"Serial [{string.Join(", ", existingSerials)}] đã tồn tại trong hệ thống.", "Cảnh báo");
                            return false;
                        }
                    }
                    else
                    {
                        var validSerialCount = db.ProductSerials.Count(item =>
                            serialNumbers.Contains(item.SerialNumber) &&
                            item.ProductId == line.SelectedProduct.Id &&
                            item.CurrentWarehouseId == WarehouseId &&
                            item.CurrentStatus == "InStock");
                        if (validSerialCount != serialNumbers.Count)
                        {
                            MessageBox.Show($"Một hoặc nhiều serial của sản phẩm {line.SelectedProduct.DisplayName} không còn tồn tại trong kho đã chọn.", "Cảnh báo");
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void RefreshData()
        {
            LoadData();
        }
    }
}
