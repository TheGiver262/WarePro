using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductViewModel : ObservableObject
    {
        private readonly ProductService _productService = new();
        private readonly ReferenceDataService _refService = new();
        private readonly DataImportManager _importManager = new();

        [ObservableProperty] private ObservableCollection<Product>  _products  = new();
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<Brand>    _brands     = new();
        [ObservableProperty] private ObservableCollection<Unit>     _units      = new();

        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private Product _currentInputProduct = new();

        public ProductViewModel()
        {
            LoadRefData();
            LoadData();
        }

        private void LoadRefData()
        {
            Categories = new ObservableCollection<Category>(_refService.GetAllCategories());
            Brands     = new ObservableCollection<Brand>(_refService.GetAllBrands());
            Units      = new ObservableCollection<Unit>(_refService.GetAllUnits());
        }

        private void LoadData()
            => Products = new ObservableCollection<Product>(_productService.GetAllProducts());

        [RelayCommand]
        private void ClearInput()
        {
            CurrentInputProduct = new Product();
            SelectedProduct = null;
        }

        [RelayCommand]
        private void SaveProduct()
        {
            if (string.IsNullOrWhiteSpace(CurrentInputProduct.Name)) return;
            if (CurrentInputProduct.CategoryId == 0 || CurrentInputProduct.BrandId == 0 || CurrentInputProduct.UnitId == 0)
            {
                System.Windows.MessageBox.Show("Vui lòng chọn Danh mục, Thương hiệu và Đơn vị tính.", "Thiếu thông tin", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (CurrentInputProduct.Id == 0)
                _productService.AddProduct(CurrentInputProduct);
            else
                _productService.UpdateProduct(CurrentInputProduct);

            LoadData();
            ClearInput();
        }

        [RelayCommand]
        private void DeleteProduct()
        {
            if (SelectedProduct == null || SelectedProduct.Id == 0) return;
            _productService.DeleteProduct(SelectedProduct.Id);
            LoadData();
            ClearInput();
        }

        [RelayCommand]
        private void AddInitialStock()
        {
            if (SelectedProduct == null)
            {
                System.Windows.MessageBox.Show("Vui lòng chọn một mặt hàng để thêm tồn kho đầu kỳ.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var win = new SerialInputWindow();
            if (win.ShowDialog() == true)
            {
                var serials = StockInService.ParseSerialRange(win.SerialInput);
                if (serials.Count > 0)
                {
                    _productService.AddInitialStock(SelectedProduct.Id, serials);
                    LoadData();
                    System.Windows.MessageBox.Show($"Đã thêm {serials.Count} serial vào tồn kho cho {SelectedProduct.Name}.", "Thành công", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
            }
        }

        [RelayCommand]
        private void ImportData()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Excel Files|*.xlsx;*.xls|CSV Files|*.csv|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var result = _importManager.ProcessFile<Product>(dialog.FileName);
                    LoadData();
                    
                    var reportWin = new ImportResultWindow(result.SuccessCount, result.Errors);
                    reportWin.ShowDialog();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(ex.Message, "Lỗi Import", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        partial void OnSelectedProductChanged(Product? value)
        {
            if (value != null)
            {
                CurrentInputProduct = new Product
                {
                    Id           = value.Id,
                    Name         = value.Name,
                    CategoryId   = value.CategoryId,
                    BrandId      = value.BrandId,
                    UnitId       = value.UnitId,
                    Quantity     = value.Quantity,
                    UnitPrice    = value.UnitPrice,
                    Origin       = value.Origin,
                    WarrantyMonths = value.WarrantyMonths,
                    Notes        = value.Notes
                };
            }
        }
    }
}
