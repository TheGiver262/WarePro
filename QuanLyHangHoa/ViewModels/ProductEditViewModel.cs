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
    public partial class ProductEditViewModel : ObservableObject
    {
        private readonly ProductService _service;
        private readonly ReferenceDataService _refDataService;

        [ObservableProperty] private Product _product;
        [ObservableProperty] private string _title;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private ObservableCollection<Unit> _units = new();

        public ProductEditViewModel(Product? product = null)
        {
            _service = new ProductService();
            _refDataService = new ReferenceDataService();
            
            _categories = new ObservableCollection<Category>(_refDataService.GetAllCategories(true));
            _brands = new ObservableCollection<Brand>(_refDataService.GetAllBrands());
            _units = new ObservableCollection<Unit>(_refDataService.GetAllUnits());

            if (product == null)
            {
                Title = "THÊM SẢN PHẨM MỚI";
                Product = new Product 
                { 
                    IsActive = true,
                    CategoryId = Categories.FirstOrDefault()?.Id ?? 0,
                    BrandId = Brands.FirstOrDefault()?.Id ?? 0,
                    DefaultUnitId = Units.FirstOrDefault()?.Id ?? 0
                };
            }
            else
            {
                Title = "CHỈNH SỬA SẢN PHẨM";
                // Clone for editing
                Product = new Product
                {
                    Id = product.Id,
                    ProductCode = product.ProductCode,
                    DisplayName = product.DisplayName,
                    CategoryId = product.CategoryId,
                    BrandId = product.BrandId,
                    DefaultUnitId = product.DefaultUnitId,
                    DefaultPrice = product.DefaultPrice,
                    OriginCountry = product.OriginCountry,
                    WarrantyPeriodMonths = product.WarrantyPeriodMonths,
                    IsSerialTracked = product.IsSerialTracked,
                    IsActive = product.IsActive
                };
            }
        }

        [RelayCommand]
        private void Confirm(Window window)
        {
            if (string.IsNullOrWhiteSpace(Product.ProductCode) || string.IsNullOrWhiteSpace(Product.DisplayName))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Mã và Tên sản phẩm!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (Product.Id == 0)
                {
                    _service.AddProduct(Product);
                }
                else
                {
                    _service.UpdateProduct(Product);
                }
                window.DialogResult = true;
                window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel(Window window)
        {
            window.DialogResult = false;
            window.Close();
        }
    }
}
