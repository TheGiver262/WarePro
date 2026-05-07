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

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockOutLineEditor : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private decimal _quantity = 1;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private ObservableCollection<ProductSerial> _selectedSerials = new();

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                Price = value.DefaultPrice;
            }
        }
    }

    public partial class StockOutViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly StockOutService _stockOutService;
        private readonly AppUser _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<StockOutLineEditor> _lines = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public StockOutViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1 };
            var factory = contextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext());
            _productService = new ProductService(factory);
            _stockOutService = new StockOutService(factory);
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            DocumentCode = $"OUT-{DateTime.Now:yyyyMMddHHmmss}";
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new StockOutLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(StockOutLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void SaveStockOut()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode) || !Lines.Any())
            {
                MessageBox.Show("Vui lòng nhập đủ thông tin.", "Cảnh báo");
                return;
            }

            try
            {
                var so = new StockOut
                {
                    DocumentCode = DocumentCode,
                    WarehouseId = WarehouseId,
                    Status = "Completed",
                    CreatedBy = _currentUser.Id,
                    CreatedAt = DateTime.Now
                };

                var soLines = Lines.Select(l => new StockOutLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    Quantity = l.Quantity,
                    BaseQuantity = l.Quantity,
                    UnitPrice = l.Price,
                    ProductSerials = l.SelectedSerials.ToList()
                }).ToList();

                _stockOutService.Create(so, soLines, _currentUser.Id);
                MessageBox.Show("Đã lưu phiếu xuất kho.", "Thông báo");
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
            DocumentCode = $"OUT-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
