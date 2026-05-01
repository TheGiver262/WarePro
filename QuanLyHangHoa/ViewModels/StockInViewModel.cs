using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockInLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _quantity = 1;
        [ObservableProperty] private decimal _price;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                Price = value.DefaultPrice;
            }
        }
    }

    public partial class StockInViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly StockInService _stockInService;
        private readonly AppUser _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<StockInLineEditor> _lines = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public StockInViewModel() : this(new AppUser { Id = 1 }) { }

        public StockInViewModel(AppUser currentUser)
        {
            _currentUser = currentUser;
            _productService = new ProductService();
            _stockInService = new StockInService();
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockInLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(StockInLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void SaveStockIn()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập đủ thông tin.", "Cảnh báo");
                return;
            }

            try
            {
                var si = new StockIn
                {
                    DocumentCode = DocumentCode,
                    WarehouseId = WarehouseId,
                    Status = "Completed",
                    CreatedBy = _currentUser.Id,
                    CreatedAt = DateTime.Now
                };

                var siLines = Lines.Select(l => new StockInLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    Quantity = l.Quantity,
                    BaseQuantity = l.Quantity,
                    UnitPrice = l.Price
                }).ToList();

                _stockInService.Create(si, siLines, _currentUser.Id);
                MessageBox.Show("Đã lưu phiếu nhập kho.", "Thông báo");
                ResetForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi");
            }
        }

        private void ResetForm()
        {
            Lines.Clear();
            DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
