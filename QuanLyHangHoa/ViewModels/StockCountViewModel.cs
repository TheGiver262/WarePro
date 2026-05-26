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
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockCountLineEditor : ObservableObject
    {
        private readonly Func<int, decimal>? _getSystemQuantity;

        public StockCountLineEditor() { }

        public StockCountLineEditor(Func<int, decimal> getSystemQuantity)
        {
            _getSystemQuantity = getSystemQuantity;
        }

        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal? _countedQuantity = null;
        [ObservableProperty] private decimal _systemQuantity;
        [ObservableProperty] private string _categoryName = string.Empty;
        [ObservableProperty] private bool _isSerialTracked = false;
        [ObservableProperty] private string _serialNumbers = string.Empty;

        public bool ShowSerialButton => IsSerialTracked && CountedQuantity.HasValue && (CountedQuantity.Value != SystemQuantity);

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                if (_getSystemQuantity != null)
                {
                    SystemQuantity = _getSystemQuantity(value.Id);
                }
                CategoryName = value.CategoryName;
                IsSerialTracked = value.IsSerialTracked;
            }
            else
            {
                SystemQuantity = 0;
                CategoryName = string.Empty;
                IsSerialTracked = false;
            }
            OnPropertyChanged(nameof(ShowSerialButton));
        }

        partial void OnCountedQuantityChanged(decimal? value)
        {
            OnPropertyChanged(nameof(ShowSerialButton));
        }

        partial void OnSystemQuantityChanged(decimal value)
        {
            OnPropertyChanged(nameof(ShowSerialButton));
        }
    }

    public partial class StockCountViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockCountService _stockCountService;
        private readonly ReferenceDataService _referenceDataService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;

        [ObservableProperty] private bool _isHistoryVisible = true;
        [ObservableProperty] private bool _isCreateNewVisible = false;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private ObservableCollection<Warehouse> _availableWarehouses = new();
        [ObservableProperty] private ObservableCollection<StockCountLineEditor> _lines = new();
        [ObservableProperty] private string _sessionCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private DateTime _countDate = DateTime.Now;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _status = "nháp";

        [ObservableProperty] private ObservableCollection<StockCountSession> _pastSessions = new();
        [ObservableProperty] private StockCountSession? _selectedSession;
        [ObservableProperty] private ObservableCollection<StockCountLine> _selectedSessionLines = new();
        [ObservableProperty] private bool _isSelectedSessionEditable = false;

        public StockCountViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1, Username = "System" };
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockCountService = new StockCountService(_contextFactory);
            _referenceDataService = new ReferenceDataService(_contextFactory);
            
            LoadData();
            AvailableWarehouses = new ObservableCollection<Warehouse>(_referenceDataService.GetAllWarehouses());
            Categories = new ObservableCollection<Category>(_referenceDataService.GetAllCategories(true));
            WarehouseId = AvailableWarehouses.FirstOrDefault(w => w.IsDefault)?.Id ?? AvailableWarehouses.FirstOrDefault()?.Id ?? 1;
            SessionCode = CreateDefaultSessionCode();
            LoadPastSessions();
        }

        partial void OnWarehouseIdChanged(int value)
        {
            UpdateSystemQuantities();
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            LoadData();
        }

        private void UpdateSystemQuantities()
        {
            using var db = _contextFactory();
            foreach (var line in Lines)
            {
                if (line.SelectedProduct != null)
                {
                    int prodId = line.SelectedProduct.Id;
                    line.SystemQuantity = db.StockBalances
                        .Where(sb => sb.WarehouseId == WarehouseId && sb.ProductId == prodId)
                        .Select(sb => sb.OnHandQuantity)
                        .FirstOrDefault();
                }
                else
                {
                    line.SystemQuantity = 0;
                }
            }
        }

        private decimal GetStockQuantity(int prodId)
        {
            using var db = _contextFactory();
            return db.StockBalances
                .Where(sb => sb.WarehouseId == WarehouseId && sb.ProductId == prodId)
                .Select(sb => sb.OnHandQuantity)
                .FirstOrDefault();
        }

        [RelayCommand]
        private void ShowCreateNew()
        {
            IsHistoryVisible = false;
            IsCreateNewVisible = true;
            Lines.Clear();
            SelectedCategory = null;
            SessionCode = CreateDefaultSessionCode();
        }

        [RelayCommand]
        private void HideCreateNew()
        {
            IsHistoryVisible = true;
            IsCreateNewVisible = false;
            Lines.Clear();
        }

        [RelayCommand]
        public void LoadData()
        {
            var products = _productService.GetAllProducts();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                products = products.Where(p => 
                    (p.DisplayName != null && p.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (p.ProductCode != null && p.ProductCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }
            if (SelectedCategory != null)
            {
                products = products.Where(p => p.CategoryId == SelectedCategory.Id).ToList();
            }
            AvailableProducts = new ObservableCollection<Product>(products);
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockCountLineEditor(prodId => GetStockQuantity(prodId)));
        }

        [RelayCommand]
        private void RemoveLine(StockCountLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            Lines.Clear();
            SelectedCategory = null;
            SessionCode = CreateDefaultSessionCode();
            IsHistoryVisible = true;
            IsCreateNewVisible = false;
        }

        [RelayCommand]
        private void OpenSerialWindow(StockCountLineEditor line)
        {
            if (line?.SelectedProduct == null) return;

            if (!line.CountedQuantity.HasValue)
            {
                MessageBox.Show("Vui lòng nhập số lượng thực tế trước khi cấu hình serial.", "Thông báo");
                return;
            }

            decimal variance = line.CountedQuantity.Value - line.SystemQuantity;
            if (variance == 0)
            {
                MessageBox.Show("Không có chênh lệch để nhập serial.", "Thông báo");
                return;
            }

            List<ProductSerial>? availableSerials = null;
            if (variance < 0)
            {
                using var db = _contextFactory();
                availableSerials = db.ProductSerials
                    .Where(ps => ps.ProductId == line.SelectedProduct.Id && ps.CurrentWarehouseId == WarehouseId && ps.CurrentStatus == "InStock")
                    .ToList();
            }

            var dialog = new SerialInputWindow(line.SerialNumbers, availableSerials, false);
            if (dialog.ShowDialog() == true)
            {
                var sns = StockInService.ParseSerialRange(dialog.SerialInput);
                var expectedCount = (int)Math.Abs(variance);
                if (sns.Count != expectedCount)
                {
                    MessageBox.Show($"Số lượng serial nhập vào ({sns.Count}) không khớp với số lượng chênh lệch ({expectedCount}).", "Lỗi nhập serial");
                    return;
                }
                line.SerialNumbers = string.Join(",", sns);
            }
        }

        [RelayCommand]
        private void OpenSessionLineSerialWindow(StockCountLine line)
        {
            if (line?.Product == null || SelectedSession == null) return;

            decimal variance = line.CountedQuantity - line.SystemQuantity;
            if (variance == 0)
            {
                MessageBox.Show("Không có chênh lệch để nhập/xem serial.", "Thông báo");
                return;
            }

            List<ProductSerial>? availableSerials = null;
            if (variance < 0)
            {
                using var db = _contextFactory();
                availableSerials = db.ProductSerials
                    .Where(ps => ps.ProductId == line.ProductId && ps.CurrentWarehouseId == SelectedSession.WarehouseId && ps.CurrentStatus == "InStock")
                    .ToList();
            }

            var dialog = new SerialInputWindow(line.SerialNumbers ?? string.Empty, availableSerials, !IsSelectedSessionEditable);
            if (IsSelectedSessionEditable)
            {
                if (dialog.ShowDialog() == true)
                {
                    var sns = StockInService.ParseSerialRange(dialog.SerialInput);
                    var expectedCount = (int)Math.Abs(variance);
                    if (sns.Count != expectedCount)
                    {
                        MessageBox.Show($"Số lượng serial nhập vào ({sns.Count}) không khớp với số lượng chênh lệch ({expectedCount}).", "Lỗi nhập serial");
                        return;
                    }
                    line.SerialNumbers = string.Join(",", sns);
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
            if (string.IsNullOrWhiteSpace(SessionCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập mã phiên và ít nhất 1 dòng kiểm kê.", "Cảnh báo");
                return;
            }

            try
            {
                using var db = _contextFactory();
                var session = new StockCountSession
                {
                    SessionCode = SessionCode,
                    WarehouseId = WarehouseId,
                    CountDate = CountDate,
                    Status = "nháp",
                    CreatedBy = _currentUser.Id
                };

                session.Lines = Lines.Select(l =>
                {
                    int prodId = l.SelectedProduct?.Id ?? 0;
                    decimal systemQty = db.StockBalances
                        .Where(sb => sb.WarehouseId == WarehouseId && sb.ProductId == prodId)
                        .Select(sb => sb.OnHandQuantity)
                        .FirstOrDefault();

                    decimal countedQty = l.CountedQuantity ?? -1m;
                    return new StockCountLine
                    {
                        ProductId = prodId,
                        CountedQuantity = countedQty,
                        SystemQuantity = systemQty,
                        VarianceQuantity = countedQty == -1m ? 0m : countedQty - systemQty,
                        SerialNumbers = l.SerialNumbers
                    };
                }).ToList();

                _stockCountService.CreateSession(session);
                MessageBox.Show("Đã lưu phiên kiểm kê ở dạng Nháp.", "Thông báo");
                
                Lines.Clear();
                SelectedCategory = null;
                SessionCode = CreateDefaultSessionCode();
                LoadPastSessions();
                IsHistoryVisible = true;
                IsCreateNewVisible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void SaveStockCount()
        {
            if (string.IsNullOrWhiteSpace(SessionCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập mã phiên và ít nhất 1 dòng kiểm kê.", "Cảnh báo");
                return;
            }

            // Kiểm tra tính hợp lệ của số lượng và serial
            foreach (var l in Lines)
            {
                if (l.SelectedProduct == null)
                {
                    MessageBox.Show("Vui lòng chọn sản phẩm cho tất cả các dòng.", "Lỗi chốt kiểm kê");
                    return;
                }

                if (!l.CountedQuantity.HasValue || l.CountedQuantity.Value < 0)
                {
                    MessageBox.Show($"Vui lòng nhập số lượng thực tế hợp lệ cho sản phẩm {l.SelectedProduct.DisplayName}.", "Lỗi chốt kiểm kê");
                    return;
                }

                decimal variance = l.CountedQuantity.Value - l.SystemQuantity;
                if (l.IsSerialTracked && variance != 0)
                {
                    var sns = StockInService.ParseSerialRange(l.SerialNumbers);
                    var expected = (int)Math.Abs(variance);
                    if (sns.Count != expected)
                    {
                        MessageBox.Show($"Sản phẩm {l.SelectedProduct.DisplayName} yêu cầu {expected} serial do chênh lệch, nhưng hiện có {sns.Count} serial.", "Lỗi chốt kiểm kê");
                        return;
                    }
                }
            }

            try
            {
                using var db = _contextFactory();
                var session = new StockCountSession
                {
                    SessionCode = SessionCode,
                    WarehouseId = WarehouseId,
                    CountDate = CountDate,
                    Status = "đã kiểm kê",
                    CreatedBy = _currentUser.Id
                };

                session.Lines = Lines.Select(l =>
                {
                    int prodId = l.SelectedProduct!.Id;
                    decimal systemQty = db.StockBalances
                        .Where(sb => sb.WarehouseId == WarehouseId && sb.ProductId == prodId)
                        .Select(sb => sb.OnHandQuantity)
                        .FirstOrDefault();

                    decimal countedQty = l.CountedQuantity!.Value;
                    return new StockCountLine
                    {
                        ProductId = prodId,
                        CountedQuantity = countedQty,
                        SystemQuantity = systemQty,
                        VarianceQuantity = countedQty - systemQty,
                        SerialNumbers = l.SerialNumbers
                    };
                }).ToList();

                _stockCountService.CreateSession(session);
                MessageBox.Show("Đã chốt phiên kiểm kê.", "Thông báo");
                
                Lines.Clear();
                SelectedCategory = null;
                SessionCode = CreateDefaultSessionCode();
                LoadPastSessions();
                IsHistoryVisible = true;
                IsCreateNewVisible = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        [RelayCommand]
        private void SaveEdit()
        {
            if (SelectedSession == null || SelectedSession.Status != "nháp") return;

            try
            {
                using var db = _contextFactory();
                
                foreach (var line in SelectedSessionLines)
                {
                    var dbLine = db.StockCountLines.Find(line.Id);
                    if (dbLine != null)
                    {
                        dbLine.CountedQuantity = line.CountedQuantity;
                        dbLine.VarianceQuantity = line.CountedQuantity == -1m ? 0m : line.CountedQuantity - dbLine.SystemQuantity;
                        dbLine.SerialNumbers = line.SerialNumbers;
                    }
                }

                db.SaveChanges();
                MessageBox.Show("Đã lưu các thay đổi của phiên kiểm kê.", "Thông báo");
                
                var currentId = SelectedSession.Id;
                SelectedSession = null;
                SelectedSession = db.StockCountSessions
                    .Include(s => s.Warehouse)
                    .Include(s => s.Creator)
                    .FirstOrDefault(s => s.Id == currentId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu chỉnh sửa: {ex.Message}", "Lỗi");
            }
        }

        [RelayCommand]
        private void CommitSession()
        {
            if (SelectedSession == null || SelectedSession.Status != "nháp") return;

            var currentId = SelectedSession.Id;

            foreach (var line in SelectedSessionLines)
            {
                if (line.CountedQuantity < 0)
                {
                    MessageBox.Show($"Sản phẩm {line.Product?.DisplayName ?? line.ProductId.ToString()} chưa được nhập số lượng thực tế.", "Lỗi chốt kiểm kê");
                    return;
                }

                decimal variance = line.CountedQuantity - line.SystemQuantity;
                if (line.Product != null && line.Product.IsSerialTracked && variance != 0)
                {
                    var sns = StockInService.ParseSerialRange(line.SerialNumbers ?? string.Empty);
                    var expected = (int)Math.Abs(variance);
                    if (sns.Count != expected)
                    {
                        MessageBox.Show($"Sản phẩm {line.Product.DisplayName} yêu cầu {expected} serial do chênh lệch, nhưng hiện có {sns.Count} serial.", "Lỗi chốt kiểm kê");
                        return;
                    }
                }
            }

            try
            {
                using var db = _contextFactory();
                
                foreach (var line in SelectedSessionLines)
                {
                    var dbLine = db.StockCountLines.Find(line.Id);
                    if (dbLine != null)
                    {
                        dbLine.CountedQuantity = line.CountedQuantity;
                        dbLine.VarianceQuantity = line.CountedQuantity - dbLine.SystemQuantity;
                        dbLine.SerialNumbers = line.SerialNumbers;
                    }
                }

                var dbSession = db.StockCountSessions.Find(currentId);
                if (dbSession != null)
                {
                    dbSession.Status = "đã kiểm kê";
                }

                db.SaveChanges();
                MessageBox.Show("Đã chốt phiên kiểm kê thành công.", "Thông báo");
                
                LoadPastSessions();
                SelectedSession = PastSessions.FirstOrDefault(s => s.Id == currentId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi chốt kiểm kê: {ex.Message}", "Lỗi");
            }
        }

        private static string CreateDefaultSessionCode()
        {
            return $"COUNT-{DateTime.Now:yyyyMMddHHmmss}";
        }

        [RelayCommand]
        public void LoadPastSessions()
        {
            try
            {
                using var db = _contextFactory();
                var sessions = db.StockCountSessions
                    .Include(s => s.Warehouse)
                    .Include(s => s.Creator)
                    .OrderByDescending(s => s.CountDate)
                    .ToList();
                PastSessions = new ObservableCollection<StockCountSession>(sessions);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải lịch sử kiểm kê: {ex.Message}", "Lỗi");
            }
        }

        partial void OnSelectedSessionChanged(StockCountSession? value)
        {
            if (value == null)
            {
                SelectedSessionLines = new ObservableCollection<StockCountLine>();
                IsSelectedSessionEditable = false;
                return;
            }

            IsSelectedSessionEditable = value.Status == "nháp";

            try
            {
                using var db = _contextFactory();
                var lines = db.StockCountLines
                    .Include(l => l.Product)
                        .ThenInclude(p => p.Category)
                    .Where(l => l.SessionId == value.Id)
                    .ToList();
                SelectedSessionLines = new ObservableCollection<StockCountLine>(lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải chi tiết phiên kiểm kê: {ex.Message}", "Lỗi");
            }
        }

        [RelayCommand]
        private void ProcessSession(StockCountSession? session)
        {
            if (session == null) return;
            if (session.Status != "đã kiểm kê")
            {
                MessageBox.Show("Chỉ có thể xử lý các phiên có trạng thái 'đã kiểm kê'.", "Cảnh báo");
                return;
            }

            var confirm = MessageBox.Show($"Bạn có muốn thực hiện xử lý chênh lệch cho phiên kiểm kê {session.SessionCode} không?", 
                "Xác nhận xử lý", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                _stockCountService.ProcessResults(session.Id, _currentUser.Id);
                MessageBox.Show("Xử lý chênh lệch thành công! Phiếu nhập/xuất điều chỉnh kho nháp đã được tạo.", "Thông báo");
                
                LoadPastSessions();
                
                var currentId = SelectedSession?.Id;
                SelectedSession = null;
                if (currentId.HasValue)
                {
                    SelectedSession = PastSessions.FirstOrDefault(s => s.Id == currentId.Value);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xử lý chênh lệch: {ex.Message}", "Lỗi");
            }
        }

        public void RefreshData()
        {
            LoadData();
            LoadPastSessions();
        }
    }
}
