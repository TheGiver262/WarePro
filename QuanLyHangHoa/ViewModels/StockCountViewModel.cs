using System;
using QuanLyHangHoa.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockCountLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _countedQuantity;
    }

    public partial class StockCountViewModel : ObservableObject
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

        public StockCountViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1, Username = "System" };
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockCountService = new StockCountService(_contextFactory);
            LoadData();
            SessionCode = CreateDefaultSessionCode();
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
                var session = new StockCountSession
                {
                    SessionCode = SessionCode,
                    WarehouseId = WarehouseId,
                    CountDate = CountDate,
                    Status = "đã kiểm kê", // Mark as counted for processing
                    CreatedBy = _currentUser.Id
                };

                session.Lines = Lines.Select(l => new StockCountLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    CountedQuantity = l.CountedQuantity,
                    // Note: SystemQuantity would normally be pulled from current balance
                    SystemQuantity = 0, 
                    VarianceQuantity = l.CountedQuantity
                }).ToList();

                _stockCountService.CreateSession(session);
                MessageBox.Show("Đã lưu phiên kiểm kê.", "Thông báo");
                
                Lines.Clear();
                SessionCode = CreateDefaultSessionCode();
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
    }
}
