using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductUnitViewModel : ObservableObject
    {
        private readonly ProductUnitService _service;
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;

        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private ObservableCollection<ProductUnit> _productUnits = new();
        [ObservableProperty] private ProductUnit? _selectedProductUnit;
        [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
        [ObservableProperty] private int _selectedUnitId;
        [ObservableProperty] private decimal _conversionFactor = 1;

        public ProductUnitViewModel()
        {
            _service = new ProductUnitService();
            _productService = new ProductService();
            _refDataService = new ReferenceDataService();
            
            LoadProducts();
            LoadUnits();
        }

        private void LoadProducts()
        {
            Products = new ObservableCollection<Product>(_productService.GetAllProducts());
        }

        private void LoadUnits()
        {
            AvailableUnits = new ObservableCollection<Unit>(_refDataService.GetAllUnits());
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                LoadProductUnits(value.Id);
            }
            else
            {
                ProductUnits.Clear();
            }
        }

        private void LoadProductUnits(int productId)
        {
            ProductUnits = new ObservableCollection<ProductUnit>(_service.GetByProductId(productId));
        }

        [RelayCommand]
        private void Save()
        {
            if (SelectedProduct == null || SelectedUnitId == 0) return;

            if (SelectedProductUnit == null)
            {
                var pu = new ProductUnit
                {
                    ProductId = SelectedProduct.Id,
                    UnitId = SelectedUnitId,
                    ConversionFactor = ConversionFactor
                };
                _service.Add(pu);
            }
            else
            {
                SelectedProductUnit.UnitId = SelectedUnitId;
                SelectedProductUnit.ConversionFactor = ConversionFactor;
                _service.Update(SelectedProductUnit);
            }

            LoadProductUnits(SelectedProduct.Id);
            Clear();
        }

        [RelayCommand]
        private void Delete()
        {
            if (SelectedProductUnit != null && SelectedProduct != null)
            {
                _service.Delete(SelectedProductUnit.Id);
                LoadProductUnits(SelectedProduct.Id);
                Clear();
            }
        }

        private void Clear()
        {
            SelectedProductUnit = null;
            SelectedUnitId = 0;
            ConversionFactor = 1;
        }

        partial void OnSelectedProductUnitChanged(ProductUnit? value)
        {
            if (value != null)
            {
                SelectedUnitId = value.UnitId;
                ConversionFactor = value.ConversionFactor;
            }
        }
    }
}
