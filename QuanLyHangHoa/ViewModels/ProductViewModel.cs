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
    public partial class ProductViewModel : ObservableObject
    {
        private readonly ProductService _service;
        private readonly ReferenceDataService _refDataService;

        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private ObservableCollection<Unit> _units = new();

        [ObservableProperty] private string _productCode = string.Empty;
        [ObservableProperty] private string _displayName = string.Empty;
        [ObservableProperty] private int _categoryId;
        [ObservableProperty] private int _brandId;
        [ObservableProperty] private int _defaultUnitId;
        [ObservableProperty] private decimal _defaultPrice;

        public ProductViewModel()
        {
            _service = new ProductService();
            _refDataService = new ReferenceDataService();
            LoadData();
        }

        private void LoadData()
        {
            Products = new ObservableCollection<Product>(_service.GetAllProducts());
            Categories = new ObservableCollection<Category>(_refDataService.GetAllCategories());
            Brands = new ObservableCollection<Brand>(_refDataService.GetAllBrands());
            Units = new ObservableCollection<Unit>(_refDataService.GetAllUnits());
        }

        [RelayCommand]
        private void Save()
        {
            if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(ProductCode)) return;

            if (SelectedProduct == null)
            {
                var p = new Product
                {
                    ProductCode = ProductCode,
                    DisplayName = DisplayName,
                    CategoryId = CategoryId,
                    BrandId = BrandId,
                    DefaultUnitId = DefaultUnitId,
                    DefaultPrice = DefaultPrice,
                    IsActive = true
                };
                _service.AddProduct(p);
            }
            else
            {
                SelectedProduct.ProductCode = ProductCode;
                SelectedProduct.DisplayName = DisplayName;
                SelectedProduct.CategoryId = CategoryId;
                SelectedProduct.BrandId = BrandId;
                SelectedProduct.DefaultUnitId = DefaultUnitId;
                SelectedProduct.DefaultPrice = DefaultPrice;
                _service.UpdateProduct(SelectedProduct);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedProduct != null)
            {
                _service.DeactivateProduct(SelectedProduct.Id);
                LoadData();
                Clear();
            }
        }

        private void Clear()
        {
            SelectedProduct = null;
            ProductCode = string.Empty;
            DisplayName = string.Empty;
            CategoryId = 0;
            BrandId = 0;
            DefaultUnitId = 0;
            DefaultPrice = 0;
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                ProductCode = value.ProductCode;
                DisplayName = value.DisplayName;
                CategoryId = value.CategoryId;
                BrandId = value.BrandId;
                DefaultUnitId = value.DefaultUnitId;
                DefaultPrice = value.DefaultPrice;
            }
        }
    }
}
