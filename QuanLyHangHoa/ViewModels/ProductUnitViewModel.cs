using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductUnitViewModel : ObservableObject
    {
        private readonly Func<List<Product>> _productLoader;
        private readonly Func<List<Unit>> _unitLoader;
        private readonly Func<int, List<ProductUnit>> _productUnitLoader;
        private readonly Action<ProductUnit> _addProductUnit;
        private readonly Action<int> _deleteProductUnit;
        private readonly Action<string, string> _showMessage;

        [ObservableProperty] private ObservableCollection<Product> _availableProducts = new();
        [ObservableProperty] private ObservableCollection<Unit> _availableUnits = new();
        [ObservableProperty] private ObservableCollection<ProductUnit> _productUnits = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private Unit? _selectedUnit;
        [ObservableProperty] private ProductUnit? _selectedProductUnit;
        [ObservableProperty] private decimal _conversionRateToBaseUnit = 1m;
        [ObservableProperty] private bool _isBaseUnit;
        [ObservableProperty] private string _statusMessage = string.Empty;

        public ProductUnitViewModel()
            : this(
                new ProductService().GetAllProducts,
                new ReferenceDataService().GetAllUnits,
                new ProductUnitService().GetProductUnits,
                new ProductUnitService().AddProductUnit,
                new ProductUnitService().DeleteProductUnit,
                (message, title) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information))
        {
        }

        public ProductUnitViewModel(
            Func<List<Product>> productLoader,
            Func<List<Unit>> unitLoader,
            Func<int, List<ProductUnit>> productUnitLoader,
            Action<ProductUnit> addProductUnit,
            Action<int> deleteProductUnit,
            Action<string, string> showMessage)
        {
            _productLoader = productLoader;
            _unitLoader = unitLoader;
            _productUnitLoader = productUnitLoader;
            _addProductUnit = addProductUnit;
            _deleteProductUnit = deleteProductUnit;
            _showMessage = showMessage;

            LoadLookups();
        }

        [RelayCommand]
        private void SaveProductUnit()
        {
            if (SelectedProduct == null || SelectedUnit == null)
            {
                StatusMessage = "Chua chon hang hoa hoac don vi.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            try
            {
                _addProductUnit(new ProductUnit
                {
                    ProductId = SelectedProduct.Id,
                    UnitId = SelectedUnit.Id,
                    ConversionRateToBaseUnit = ConversionRateToBaseUnit,
                    IsBaseUnit = IsBaseUnit
                });

                StatusMessage = "Da luu don vi quy doi.";
                _showMessage(StatusMessage, "Thong bao");
                ConversionRateToBaseUnit = 1m;
                IsBaseUnit = false;
                LoadProductUnits();
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = ex.Message;
                _showMessage(ex.Message, "Loi don vi san pham");
            }
        }

        [RelayCommand]
        private void DeleteProductUnit()
        {
            if (SelectedProductUnit == null)
            {
                StatusMessage = "Chua chon don vi quy doi.";
                _showMessage(StatusMessage, "Canh bao");
                return;
            }

            _deleteProductUnit(SelectedProductUnit.Id);
            StatusMessage = "Da xoa don vi quy doi.";
            _showMessage(StatusMessage, "Thong bao");
            LoadProductUnits();
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            LoadProductUnits();
        }

        private void LoadLookups()
        {
            AvailableProducts = new ObservableCollection<Product>(_productLoader());
            AvailableUnits = new ObservableCollection<Unit>(_unitLoader());
        }

        private void LoadProductUnits()
        {
            if (SelectedProduct == null)
            {
                ProductUnits = new ObservableCollection<ProductUnit>();
                return;
            }

            ProductUnits = new ObservableCollection<ProductUnit>(_productUnitLoader(SelectedProduct.Id));
        }
    }
}
