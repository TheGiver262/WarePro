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
using ClosedXML.Excel;
using Microsoft.Win32;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockTransferLineEditor : ObservableObject
    {
        private readonly ProductUnitService _productUnitService;

        [ObservableProperty] private int _id;
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSerialComplete))] private decimal _quantity = 1;
        [ObservableProperty] private Unit? _selectedUnit;
        [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
        [ObservableProperty] private ObservableCollection<string> _serialNumbers = new();
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsSerialComplete))] private bool _isSerialRequired;
        [ObservableProperty] private decimal _baseQuantity;

        public string SerialSummary => SerialNumbers.Count > 0 ? $"{SerialNumbers.Count} Serial" : "Chưa có Serial";
        public bool IsSerialComplete => !IsSerialRequired || SerialNumbers.Count == (int)Quantity;

        public StockTransferLineEditor(ProductUnitService productUnitService)
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

    public partial class StockTransferViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockTransferService _stockTransferService;
        private readonly ProductUnitService _productUnitService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();
        [ObservableProperty] private ObservableCollection<StockTransferLineEditor> _lines = new();

        [ObservableProperty] private int _stockTransferId;
        [ObservableProperty] private string _status = "Draft";
        [ObservableProperty] private bool _isPosted;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private Warehouse? _selectedFromWarehouse;
        [ObservableProperty] private Warehouse? _selectedToWarehouse;
        [ObservableProperty] private DateTime _transferDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;

        // List view properties
        [ObservableProperty] private ObservableCollection<StockTransfer> _stockTransferList = new();
        [ObservableProperty] private bool _isListViewVisible = true;
        [ObservableProperty] private bool _isDetailViewVisible = false;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "Tất cả";
        [ObservableProperty] private ObservableCollection<string> _availableStatuses = new() { "Tất cả", "Phiếu nháp", "Đã ghi sổ" };

        // Footer stats
        [ObservableProperty] private int _totalCount;
        [ObservableProperty] private int _draftCount;

        public bool CanEdit => !IsPosted;

        public StockTransferViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1 };
            var factory = contextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext());
            _contextFactory = factory;
            _productService = new ProductService(factory);
            _stockTransferService = new StockTransferService(factory);
            _productUnitService = new ProductUnitService(factory);
            var refDataService = new ReferenceDataService(factory);

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableWarehouses = new ObservableCollection<Warehouse>(refDataService.GetAllWarehouses());
            
            SelectedFromWarehouse = AvailableWarehouses.FirstOrDefault(w => w.IsDefault) ?? AvailableWarehouses.FirstOrDefault();
            SelectedToWarehouse = AvailableWarehouses.FirstOrDefault(w => !w.IsDefault) ?? AvailableWarehouses.ElementAtOrDefault(1);
            
            DocumentCode = $"ST-{DateTime.Now:yyyyMMddHHmmss}";

            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var all = _stockTransferService.GetAll();
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                all = all.Where(s => s.DocumentCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase) || 
                                    (s.Notes?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
            }

            if (SelectedStatus != "Tất cả")
            {
                string statusValue = SelectedStatus == "Phiếu nháp" ? "Draft" : "Posted";
                all = all.Where(s => s.Status == statusValue).ToList();
            }

            StockTransferList = new ObservableCollection<StockTransfer>(all);
            TotalCount = all.Count;
            DraftCount = all.Count(s => s.Status == "Draft");
        }

        [RelayCommand]
        private void ResetFilter()
        {
            SearchText = string.Empty;
            SelectedStatus = "Tất cả";
            LoadData();
        }

        [RelayCommand]
        private void CreateNew()
        {
            ResetForm();
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void ViewDetail(StockTransfer st)
        {
            if (st == null) return;
            LoadFromModel(st);
            IsListViewVisible = false;
            IsDetailViewVisible = true;
        }

        [RelayCommand]
        private void EditDetail(StockTransfer st)
        {
            if (st == null) return;
            if (st.Status == "Posted")
            {
                MessageBox.Show("Không thể sửa phiếu đã ghi sổ.", "Thông báo");
                return;
            }
            LoadFromModel(st);
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

        private void LoadFromModel(StockTransfer st)
        {
            StockTransferId = st.Id;
            DocumentCode = st.DocumentCode;
            SelectedFromWarehouse = AvailableWarehouses.FirstOrDefault(w => w.Id == st.FromWarehouseId);
            SelectedToWarehouse = AvailableWarehouses.FirstOrDefault(w => w.Id == st.ToWarehouseId);
            TransferDate = st.TransferDate;
            Notes = st.Notes ?? string.Empty;
            Status = st.Status;
            IsPosted = st.Status == "Posted";
            
            Lines.Clear();
            foreach (var line in st.Lines)
            {
                var editor = new StockTransferLineEditor(_productUnitService)
                {
                    Id = line.Id,
                    SelectedProduct = line.Product,
                    Quantity = line.Quantity,
                    SelectedUnit = line.Unit
                };
                
                foreach (var sn in line.ProductSerials)
                {
                    editor.SerialNumbers.Add(sn.SerialNumber);
                }
                
                Lines.Add(editor);
            }
            
            OnPropertyChanged(nameof(CanEdit));
        }

        [RelayCommand]
        private void AddLine()
        {
            if (!CanEdit) return;
            Lines.Add(new StockTransferLineEditor(_productUnitService));
        }

        [RelayCommand]
        private void RemoveLine(StockTransferLineEditor line)
        {
            if (!CanEdit) return;
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void OpenSerialInput(StockTransferLineEditor line)
        {
            if (line == null || line.SelectedProduct == null) return;
            
            var isAdmin = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageUsers);
            var existing = string.Join("\n", line.SerialNumbers);
            var isReadOnly = !CanEdit && !isAdmin;

            List<ProductSerial> available = null;
            if (line.SelectedProduct != null && SelectedFromWarehouse != null)
            {
                using (var db = _contextFactory())
                {
                    var query = db.ProductSerials
                        .Where(s => s.ProductId == line.SelectedProduct.Id && s.CurrentWarehouseId == SelectedFromWarehouse.Id && s.CurrentStatus == "InStock");
                    
                    if (!CanEdit && isAdmin)
                    {
                        var lineSerialsQuery = db.ProductSerials
                            .Where(s => s.ProductId == line.SelectedProduct.Id && s.StockTransferLineId == line.Id);
                        
                        var inStockList = query.ToList();
                        var lineList = lineSerialsQuery.ToList();
                        
                        var existIds = new HashSet<int>(inStockList.Select(a => a.Id));
                        foreach (var ls in lineList)
                        {
                            if (!existIds.Contains(ls.Id))
                            {
                                inStockList.Add(ls);
                            }
                        }
                        available = inStockList;
                    }
                    else
                    {
                        available = query.ToList();
                    }
                }
            }

            var dialog = new Views.SerialInputWindow(existing, available, isReadOnly);
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
                            var dbLine = db.StockTransferLines
                                .Include(x => x.StockTransfer)
                                .FirstOrDefault(x => x.Id == line.Id);

                            if (dbLine == null)
                            {
                                MessageBox.Show("Không tìm thấy dòng phiếu chuyển kho trong cơ sở dữ liệu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }

                            var fromWarehouseId = dbLine.StockTransfer.FromWarehouseId;
                            var toWarehouseId = dbLine.StockTransfer.ToWarehouseId;

                            var currentSerials = db.ProductSerials
                                .Where(x => x.StockTransferLineId == dbLine.Id)
                                .ToList();

                            var currentSnList = currentSerials.Select(s => s.SerialNumber).ToList();

                            var removedSnList = currentSnList.Except(newSerials, StringComparer.OrdinalIgnoreCase).ToList();
                            var addedSnList = newSerials.Except(currentSnList, StringComparer.OrdinalIgnoreCase).ToList();

                            // Validate các serial bị loại bỏ
                            foreach (var sn in removedSnList)
                            {
                                var ps = currentSerials.FirstOrDefault(x => x.SerialNumber.Equals(sn, StringComparison.OrdinalIgnoreCase));
                                if (ps != null)
                                {
                                    if (ps.CurrentStatus != SerialStatus.InStock.ToString() || ps.CurrentWarehouseId != toWarehouseId)
                                    {
                                        MessageBox.Show($"Không thể thu hồi serial {sn} vì nó đã được xuất hoặc bán khỏi kho đến.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                if (ps.CurrentStatus != SerialStatus.InStock.ToString() || ps.CurrentWarehouseId != fromWarehouseId)
                                {
                                    MessageBox.Show($"Số serial {sn} không có sẵn (InStock) tại kho đi của phiếu.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            // Thực hiện cập nhật
                            foreach (var sn in removedSnList)
                            {
                                var ps = currentSerials.FirstOrDefault(x => x.SerialNumber.Equals(sn, StringComparison.OrdinalIgnoreCase));
                                if (ps != null)
                                {
                                    ps.CurrentWarehouseId = fromWarehouseId;
                                    ps.StockTransferLineId = null;
                                }
                            }

                            foreach (var sn in addedSnList)
                            {
                                var ps = db.ProductSerials.FirstOrDefault(x => x.SerialNumber == sn && x.ProductId == dbLine.ProductId);
                                if (ps != null)
                                {
                                    ps.CurrentWarehouseId = toWarehouseId;
                                    ps.StockTransferLineId = dbLine.Id;
                                }
                            }

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
        private void SaveDraft()
        {
            if (!ValidateForm()) return;

            try
            {
                var st = CreateModel();
                var stLines = CreateLines();

                _stockTransferService.SaveDraft(st, stLines, _currentUser.Id);
                StockTransferId = st.Id;
                Status = st.Status;
                
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

            if (!ValidateForm()) return;
            try
            {
                var st = CreateModel();
                var stLines = CreateLines();
                _stockTransferService.SaveDraft(st, stLines, _currentUser.Id);
                StockTransferId = st.Id;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu dữ liệu trước khi ghi sổ: {ex.Message}", "Lỗi");
                return;
            }

            foreach (var line in Lines)
            {
                if (line.IsSerialRequired && line.SerialNumbers.Count != (int)line.Quantity)
                {
                    var result = MessageBox.Show(
                        $"Sản phẩm {line.SelectedProduct?.DisplayName} yêu cầu {(int)line.Quantity} serial, nhưng hiện mới có {line.SerialNumbers.Count}.\n\nBạn có muốn bổ sung serial trước khi ghi sổ không?", 
                        "Thiếu Serial", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    
                    if (result == MessageBoxResult.Yes) return;
                }
            }

            var confirm = MessageBox.Show("Bạn có chắc chắn muốn ghi sổ phiếu chuyển kho này? Sau khi ghi sổ sẽ không thể chỉnh sửa.", "Xác nhận ghi sổ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _stockTransferService.Post(StockTransferId, _currentUser.Id);
                IsPosted = true;
                Status = "Posted";
                OnPropertyChanged(nameof(CanEdit));
                
                MessageBox.Show("Đã ghi sổ thành công. Hàng hóa đã được chuyển kho.", "Thông báo");
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
            if (StockTransferList == null || !StockTransferList.Any())
            {
                MessageBox.Show("Không có dữ liệu để xuất.", "Thông báo");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"ChuyenKho_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var worksheet = workbook.Worksheets.Add("PhieuChuyenKho");

                        // Headers
                        worksheet.Cell(1, 1).Value = "MÃ PHIẾU";
                        worksheet.Cell(1, 2).Value = "KHO ĐI";
                        worksheet.Cell(1, 3).Value = "KHO ĐẾN";
                        worksheet.Cell(1, 4).Value = "NGÀY CHUYỂN";
                        worksheet.Cell(1, 5).Value = "TRẠNG THÁI";
                        worksheet.Cell(1, 6).Value = "GHI CHÚ";

                        // Data
                        int row = 2;
                        foreach (var item in StockTransferList)
                        {
                            worksheet.Cell(row, 1).Value = item.DocumentCode;
                            worksheet.Cell(row, 2).Value = item.FromWarehouse?.DisplayName;
                            worksheet.Cell(row, 3).Value = item.ToWarehouse?.DisplayName;
                            worksheet.Cell(row, 4).Value = item.TransferDate;
                            worksheet.Cell(row, 5).Value = item.Status == "Draft" ? "Phiếu nháp" : "Đã ghi sổ";
                            worksheet.Cell(row, 6).Value = item.Notes;
                            row++;
                        }

                        // Formatting
                        var headerRange = worksheet.Range(1, 1, 1, 6);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#4F46E5");
                        headerRange.Style.Font.FontColor = XLColor.White;
                        worksheet.Columns().AdjustToContents();

                        workbook.SaveAs(saveFileDialog.FileName);
                    }
                    MessageBox.Show("Xuất file Excel thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi");
                }
            }
        }

        private bool ValidateForm()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập đủ thông tin sản phẩm.", "Cảnh báo");
                return false;
            }

            if (SelectedFromWarehouse == null || SelectedToWarehouse == null)
            {
                MessageBox.Show("Vui lòng chọn kho đi và kho đến.", "Cảnh báo");
                return false;
            }

            if (SelectedFromWarehouse.Id == SelectedToWarehouse.Id)
            {
                MessageBox.Show("Kho đi và kho đến phải khác nhau.", "Cảnh báo");
                return false;
            }

            return true;
        }

        private StockTransfer CreateModel()
        {
            return new StockTransfer
            {
                Id = StockTransferId,
                DocumentCode = DocumentCode,
                FromWarehouseId = SelectedFromWarehouse?.Id ?? 0,
                ToWarehouseId = SelectedToWarehouse?.Id ?? 0,
                TransferDate = TransferDate,
                Notes = Notes,
                Status = "Draft",
                CreatedBy = _currentUser.Id,
                CreatedAt = DateTime.Now
            };
        }

        private List<StockTransferLine> CreateLines()
        {
            return Lines.Select(l => new StockTransferLine
            {
                ProductId = l.SelectedProduct?.Id ?? 0,
                UnitId = l.SelectedUnit?.Id ?? 0,
                Quantity = l.Quantity,
                BaseQuantity = _productUnitService.ConvertToBaseUnit(l.SelectedProduct?.Id ?? 0, l.SelectedUnit?.Id ?? 0, l.Quantity),
                ProductSerials = l.SerialNumbers.Select(sn => new ProductSerial 
                { 
                    SerialNumber = sn, 
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    CurrentWarehouseId = SelectedFromWarehouse?.Id,
                    CurrentStatus = "InStock"
                }).ToList()
            }).ToList();
        }

        [RelayCommand]
        private void Cancel()
        {
            BackToList();
        }

        private void ResetForm()
        {
            StockTransferId = 0;
            Status = "Draft";
            IsPosted = false;
            OnPropertyChanged(nameof(CanEdit));
            
            Lines.Clear();
            DocumentCode = $"ST-{DateTime.Now:yyyyMMddHHmmss}";
            TransferDate = DateTime.Now;
            Notes = string.Empty;
        }

        public void RefreshData()
        {
            LoadData();
        }
    }
}
