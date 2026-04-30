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
    public partial class InvoiceLineEditor : ObservableObject
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

    public partial class InvoiceViewModel : ObservableObject
    {
        private readonly ProductService _productService;
        private readonly AppUser _currentUser;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts;
        [ObservableProperty] private ObservableCollection<InvoiceLineEditor> _lines = new();
        [ObservableProperty] private string _invoiceCode = string.Empty;
        [ObservableProperty] private int _customerId;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public InvoiceViewModel() : this(new AppUser { Id = 1 }) { }

        public InvoiceViewModel(AppUser currentUser)
        {
            _currentUser = currentUser;
            _productService = new ProductService();
            AvailableProducts = new ObservableCollection<Product>(_productService.GetAllProducts());
            InvoiceCode = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        }

        [RelayCommand]
        private void AddLine()
        {
            Lines.Add(new InvoiceLineEditor());
        }

        [RelayCommand]
        private void RemoveLine(InvoiceLineEditor line)
        {
            if (line != null)
            {
                Lines.Remove(line);
            }
        }

        [RelayCommand]
        private void SaveInvoice()
        {
            MessageBox.Show("Chức năng hóa đơn đang được tích hợp với hệ thống kho mới.", "Thông báo");
        }
    }
}
