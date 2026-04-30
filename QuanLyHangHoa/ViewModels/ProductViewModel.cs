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

        // Object for editing/input
        [ObservableProperty] private Product _currentInputProduct = new();

        [ObservableProperty] private string _searchText = string.Empty;

        public ProductViewModel()
        {
            _service = new ProductService();
            _refDataService = new ReferenceDataService();
            LoadData();
            Clear();
        }

        private void LoadData()
        {
            var results = _service.GetAllProducts();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLower();
                results = results.Where(p => 
                    p.DisplayName.ToLower().Contains(term) || 
                    p.ProductCode.ToLower().Contains(term)).ToList();
            }
            Products = new ObservableCollection<Product>(results);
            
            Categories = new ObservableCollection<Category>(_refDataService.GetAllCategories());
            Brands = new ObservableCollection<Brand>(_refDataService.GetAllBrands());
            Units = new ObservableCollection<Unit>(_refDataService.GetAllUnits());
        }

        [RelayCommand]
        private void Search() => LoadData();

        [RelayCommand]
        private void SaveProduct()
        {
            if (string.IsNullOrWhiteSpace(CurrentInputProduct.DisplayName) || 
                string.IsNullOrWhiteSpace(CurrentInputProduct.ProductCode)) 
            {
                MessageBox.Show("Vui lòng nhập đầy đủ Mã và Tên sản phẩm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedProduct == null)
            {
                CurrentInputProduct.IsActive = true;
                _service.AddProduct(CurrentInputProduct);
            }
            else
            {
                _service.UpdateProduct(CurrentInputProduct);
            }
            LoadData();
            Clear();
        }

        [RelayCommand]
        private void DeleteProduct()
        {
            if (SelectedProduct != null)
            {
                var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá sản phẩm '{SelectedProduct.DisplayName}'?", 
                    "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                if (result == MessageBoxResult.Yes)
                {
                    _service.DeactivateProduct(SelectedProduct.Id);
                    LoadData();
                    Clear();
                }
            }
        }

        [RelayCommand]
        private void ClearInput() => Clear();

        private void Clear()
        {
            SelectedProduct = null;
            CurrentInputProduct = new Product 
            { 
                IsActive = true,
                CategoryId = Categories.FirstOrDefault()?.Id ?? 0,
                BrandId = Brands.FirstOrDefault()?.Id ?? 0,
                DefaultUnitId = Units.FirstOrDefault()?.Id ?? 0
            };
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                // Create a copy for editing to avoid direct binding updates to the list item before save
                CurrentInputProduct = new Product
                {
                    Id = value.Id,
                    ProductCode = value.ProductCode,
                    DisplayName = value.DisplayName,
                    CategoryId = value.CategoryId,
                    BrandId = value.BrandId,
                    DefaultUnitId = value.DefaultUnitId,
                    DefaultPrice = value.DefaultPrice,
                    OriginCountry = value.OriginCountry,
                    WarrantyPeriodMonths = value.WarrantyPeriodMonths,
                    IsSerialTracked = value.IsSerialTracked,
                    IsActive = value.IsActive
                };
            }
            else
            {
                Clear();
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            LoadData();
        }
    }
}
