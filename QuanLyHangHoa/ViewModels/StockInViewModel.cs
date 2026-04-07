using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;

namespace QuanLyHangHoa.ViewModels
{
    public partial class StockInDetailWrapper : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private decimal _importPrice;
        [ObservableProperty] private string _serialInputString = string.Empty;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null) {
                ImportPrice = value.UnitPrice * 0.8m; // Default guess
            }
        }
    }

    public partial class StockInViewModel : ObservableObject
    {
        private readonly StockInService _stockInService;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;
        private readonly Employee _currentUser;

        [ObservableProperty]
        private ObservableCollection<Product> _availableProducts;

        [ObservableProperty]
        private ObservableCollection<Supplier> _availableSuppliers;

        [ObservableProperty]
        private Supplier? _selectedSupplier;

        [ObservableProperty]
        private DateTime _importDate = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<StockInDetailWrapper> _details;

        public StockInViewModel(Employee currentUser)
        {
            _currentUser = currentUser;
            _stockInService = new StockInService();
            _productService = new ProductService();
            _refDataService = new ReferenceDataService();

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableSuppliers = new ObservableCollection<Supplier>(_refDataService.GetAllSuppliers());
            Details = new ObservableCollection<StockInDetailWrapper>();
        }

        [RelayCommand]
        private void AddDetail()
        {
            Details.Add(new StockInDetailWrapper());
        }

        [RelayCommand]
        private void RemoveDetail(StockInDetailWrapper detail)
        {
            if (detail != null) Details.Remove(detail);
        }

        [RelayCommand]
        private void OpenSerialInput(StockInDetailWrapper detail)
        {
            if (detail == null) return;
            var window = new SerialInputWindow(detail.SerialInputString);
            if (window.ShowDialog() == true)
            {
                detail.SerialInputString = window.SerialInput;
                var parsedSerials = StockInService.ParseSerialRange(window.SerialInput);
                detail.Quantity = parsedSerials.Count;
            }
        }

        [RelayCommand]
        private void SaveStockIn()
        {
            if (SelectedSupplier == null)
            {
                MessageBox.Show("Vui lòng chọn nhà cung cấp!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Details.Any() || Details.Any(d => d.SelectedProduct == null))
            {
                MessageBox.Show("Vui lòng chọn sản phẩm cho tất cả các dòng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (Details.Any(d => d.Quantity <= 0))
            {
                MessageBox.Show("Số lượng nhập phải lớn hơn 0!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var stockIn = new StockIn
            {
                EmployeeId = _currentUser.Id,
                SupplierId = SelectedSupplier.Id,
                ImportDate = ImportDate
            };

            foreach (var detailWrapper in Details)
            {
                var detail = new StockInDetail
                {
                    ProductId = detailWrapper.SelectedProduct!.Id,
                    Quantity = detailWrapper.Quantity,
                    ImportPrice = detailWrapper.ImportPrice
                };

                // Generate ProductSerials based on the input string
                var parsedSerials = StockInService.ParseSerialRange(detailWrapper.SerialInputString);
                
                // If serials were provided, use them. Otherwise, generate dummy serials if quantity is provided but no exact serial string
                if (parsedSerials.Count > 0)
                {
                    foreach (var s in parsedSerials)
                    {
                        detail.ProductSerials.Add(new ProductSerial { SerialNumber = s, ProductId = detail.ProductId, Status = "InStock" });
                    }
                }
                else
                {
                    for (int i = 0; i < detail.Quantity; i++)
                    {
                        detail.ProductSerials.Add(new ProductSerial { SerialNumber = $"AUTO-{DateTime.Now.Ticks}-{i}", ProductId = detail.ProductId, Status = "InStock" });
                    }
                }

                stockIn.StockInDetails.Add(detail);
            }

            _stockInService.Create(stockIn);
            MessageBox.Show("Lưu Phiếu Nhập Kho Thành Công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            
            Details.Clear();
            SelectedSupplier = null;
        }
    }
}
