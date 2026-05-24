using System;
using QuanLyHangHoa.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockCountLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _countedQuantity;
    }

    public partial class StockCountViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly StockCountService _stockCountService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
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

        public StockCountViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1, Username = "System" };
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockCountService = new StockCountService(_contextFactory);
            LoadData();
            SessionCode = CreateDefaultSessionCode();
            LoadPastSessions();
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
            AvailableProducts = new ObservableCollection<Product>(products);
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockCountLineEditor());
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
            SessionCode = CreateDefaultSessionCode();
        }

        [RelayCommand]
        private void SaveStockCount()
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
                    Status = "đã kiểm kê", // Mark as counted for processing
                    CreatedBy = _currentUser.Id
                };

                session.Lines = Lines.Select(l =>
                {
                    int prodId = l.SelectedProduct?.Id ?? 0;
                    decimal systemQty = db.StockBalances
                        .Where(sb => sb.WarehouseId == WarehouseId && sb.ProductId == prodId)
                        .Select(sb => sb.OnHandQuantity)
                        .FirstOrDefault();

                    return new StockCountLine
                    {
                        ProductId = prodId,
                        CountedQuantity = l.CountedQuantity,
                        SystemQuantity = systemQty,
                        VarianceQuantity = l.CountedQuantity - systemQty
                    };
                }).ToList();

                _stockCountService.CreateSession(session);
                MessageBox.Show("Đã lưu phiên kiểm kê.", "Thông báo");
                
                Lines.Clear();
                SessionCode = CreateDefaultSessionCode();
                LoadPastSessions();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
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
                return;
            }

            try
            {
                using var db = _contextFactory();
                var lines = db.StockCountLines
                    .Include(l => l.Product)
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
                MessageBox.Show("Xử lý chênh lệch thành công! Kho đã được điều chỉnh.", "Thông báo");
                
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
