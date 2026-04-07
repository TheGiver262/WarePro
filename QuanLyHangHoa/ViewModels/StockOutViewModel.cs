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
    public partial class StockOutDetailWrapper : ObservableObject
    {
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private int _quantity;
        [ObservableProperty] private decimal _exportPrice;
        [ObservableProperty] private string _serialInputString = string.Empty;

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null) {
                ExportPrice = value.UnitPrice;
            }
        }
    }

    public partial class StockOutViewModel : ObservableObject
    {
        private readonly StockOutService _stockOutService;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;
        private readonly Employee _currentUser;

        [ObservableProperty]
        private ObservableCollection<Product> _availableProducts;

        [ObservableProperty]
        private ObservableCollection<Customer> _availableCustomers;

        [ObservableProperty]
        private Customer? _selectedCustomer;

        [ObservableProperty]
        private DateTime _exportDate = DateTime.Now;

        [ObservableProperty]
        private ObservableCollection<StockOutDetailWrapper> _details;

        public StockOutViewModel(Employee currentUser)
        {
            _currentUser = currentUser;
            _stockOutService = new StockOutService();
            _productService = new ProductService();
            _refDataService = new ReferenceDataService();

            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            AvailableCustomers = new ObservableCollection<Customer>(_refDataService.GetAllCustomers());
            Details = new ObservableCollection<StockOutDetailWrapper>();
        }

        [RelayCommand]
        private void AddDetail()
        {
            Details.Add(new StockOutDetailWrapper());
        }

        [RelayCommand]
        private void RemoveDetail(StockOutDetailWrapper detail)
        {
            if (detail != null) Details.Remove(detail);
        }

        [RelayCommand]
        private void OpenSerialInput(StockOutDetailWrapper detail)
        {
            if (detail == null || detail.SelectedProduct == null) 
            {
                MessageBox.Show("Vui lòng chọn sản phẩm trước!", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var availableSerials = _stockOutService.GetInStockSerials(detail.SelectedProduct.Id);
            var window = new SerialInputWindow(detail.SerialInputString, availableSerials);
            
            if (window.ShowDialog() == true)
            {
                detail.SerialInputString = window.SerialInput;
                var parsedSerials = StockInService.ParseSerialRange(window.SerialInput);
                detail.Quantity = parsedSerials.Count;
            }
        }

        [RelayCommand]
        private void SaveStockOut()
        {
            if (SelectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn khách hàng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Details.Any() || Details.Any(d => d.SelectedProduct == null))
            {
                MessageBox.Show("Vui lòng chọn sản phẩm cho tất cả các dòng!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (Details.Any(d => d.Quantity <= 0))
            {
                MessageBox.Show("Số lượng xuất phải lớn hơn 0! Bạn đã nhập chuẩn Serials chưa?", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var stockOut = new StockOut
            {
                EmployeeId = _currentUser.Id,
                CustomerId = SelectedCustomer.Id,
                ExportDate = ExportDate
            };

            foreach (var detailWrapper in Details)
            {
                var detail = new StockOutDetail
                {
                    ProductId = detailWrapper.SelectedProduct!.Id,
                    Quantity = detailWrapper.Quantity,
                    ExportPrice = detailWrapper.ExportPrice
                };

                // Parse the serial string directly from wrapper into detail
                var parsedSerials = StockInService.ParseSerialRange(detailWrapper.SerialInputString);
                
                // Add the serials we mapped from the popup
                foreach (var s in parsedSerials)
                {
                    detail.ProductSerials.Add(new ProductSerial { SerialNumber = s, ProductId = detail.ProductId });
                }

                stockOut.StockOutDetails.Add(detail);
            }

            _stockOutService.Create(stockOut);
            MessageBox.Show("Lưu Phiếu Xuất Kho Thành Công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            
            Details.Clear();
            SelectedCustomer = null;
        }
    }
}
