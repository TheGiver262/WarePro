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
        [ObservableProperty] private ObservableCollection<Supplier> _availableSuppliers = new();
        [ObservableProperty] private Supplier? _selectedSupplier;
        [ObservableProperty] private DateTime _importDate = DateTime.Now;
        [ObservableProperty] private string _notes = string.Empty;

        public decimal TotalAmount => Lines.Sum(l => l.Quantity * l.Price);

        public StockInViewModel(AppUser? currentUser = null, Func<AppDbContext>? contextFactory = null)
        {
            _currentUser = currentUser ?? new AppUser { Id = 1 };
            var factory = contextFactory ?? (() => new QuanLyHangHoa.Data.AppDbContext());
            _productService = new ProductService(factory);
            _stockInService = new StockInService(factory);
            var supplierService = new SupplierService(factory);

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableSuppliers = new ObservableCollection<Supplier>(supplierService.GetAll());
            DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";

            Lines.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalAmount));
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
                    SupplierId = SelectedSupplier?.Id,
                    ImportDate = ImportDate,
                    Notes = Notes,
                    Status = "Completed",
                    CreatedBy = _currentUser.Id,
                    CreatedAt = DateTime.Now,
                    PurposeCode = "Import"
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

        [RelayCommand]
        private void Cancel()
        {
            ResetForm();
        }

        private void ResetForm()
        {
            Lines.Clear();
            DocumentCode = $"IN-{DateTime.Now:yyyyMMddHHmmss}";
        }
    }
}
