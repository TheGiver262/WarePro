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
        private readonly CustomerService _customerService;
        private readonly AppUser _currentUser;
        private readonly Func<AppDbContext> _contextFactory;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<Customer> _availableCustomers;
        [ObservableProperty] private ObservableCollection<StockOutLineEditor> _lines = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _documentCode = string.Empty;
        [ObservableProperty] private int _warehouseId = 1;
        [ObservableProperty] private DateTime _exportDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private Customer? _selectedCustomer;
        [ObservableProperty] private decimal _totalAmount;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public StockOutViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1, Username = "System" };
            _contextFactory = contextFactory ?? (() => new AppDbContext());
            _productService = new ProductService(_contextFactory);
            _stockOutService = new StockOutService(_contextFactory);
            _customerService = new CustomerService(_contextFactory);

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableCustomers = new ObservableCollection<Customer>(_customerService.GetAll());
            DocumentCode = $"OUT-{DateTime.Now:yyyyMMddHHmmss}";
            
            Lines.CollectionChanged += (s, e) => RecalculateTotal();
        }

        private void RecalculateTotal()
        {
            TotalAmount = Lines.Sum(l => l.Quantity * l.Price);
        }

        [RelayCommand]
        private void AddLine()
        {
            var newLine = new StockOutLineEditor();
            newLine.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(StockOutLineEditor.Quantity) || e.PropertyName == nameof(StockOutLineEditor.Price))
                    RecalculateTotal();
            };
            Lines.Add(newLine);
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
        private void Cancel()
        {
            ResetForm();
        }

        [RelayCommand]
        private void OpenSerialInput(StockOutLineEditor line)
        {
            // Placeholder for serial selection logic
            MessageBox.Show($"Chọn Serial cho {line.SelectedProduct?.DisplayName ?? "sản phẩm"}", "Serial Input");
        }

        [RelayCommand]
        private void SaveStockOut()
        {
            if (string.IsNullOrWhiteSpace(DocumentCode) || !Lines.Any() || SelectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn khách hàng và ít nhất một sản phẩm.", "Cảnh báo");
                return;
            }

            try
            {
                var so = new StockOut
                {
                    DocumentCode = DocumentCode,
                    WarehouseId = WarehouseId,
                    CustomerId = SelectedCustomer.Id,
                    ExportDate = ExportDate,
                    Notes = Notes,
                    Status = "Completed",
                    PurposeCode = "SALE", // Default purpose
                    CreatedBy = _currentUser.Id,
                    CreatedAt = DateTime.Now
                };

                var soLines = Lines.Select(l => new StockOutLine
                {
                    ProductId = l.SelectedProduct?.Id ?? 0,
                    Quantity = l.Quantity,
                    BaseQuantity = l.Quantity,
                    UnitPrice = l.Price,
                    UnitId = l.SelectedProduct?.DefaultUnitId ?? 1, // Default to 1 if not set
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
            Notes = string.Empty;
            SelectedCustomer = null;
            ExportDate = DateTime.Now;
        }
    }
}
