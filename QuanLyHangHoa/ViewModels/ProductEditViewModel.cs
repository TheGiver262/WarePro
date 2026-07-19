using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using System.Text.Json;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductEditViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly ProductService _service;
        private readonly ReferenceDataService _refDataService;
        private readonly AppUser _currentUser;
        private string? _beforeJson;
        private readonly byte[]? _expectedRowVersion;

        [ObservableProperty] private Product _product;
        [ObservableProperty] private string _title;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private ObservableCollection<Unit> _units = new();

        public ProductEditViewModel(Func<AppDbContext> contextFactory, AppUser currentUser, Product? product = null)
        {
            _contextFactory = contextFactory;
            _currentUser = currentUser;
            _service = new ProductService(_contextFactory);
            _refDataService = new ReferenceDataService(_contextFactory);
            
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
                _beforeJson = null;
                _expectedRowVersion = null;
            }
            else
            {
                Title = "CHỈNH SỬA SẢN PHẨM";
                _beforeJson = Serialize(product);
                _expectedRowVersion = product.RowVersion.ToArray();

                // clone giữ cửa sổ chỉnh sửa độc lập; Cancel không làm đổi row đang bind
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
        // form kiểm tra mã/tên; service xử lý insert/update, audit và transaction theo actor hiện tại
        private async Task Confirm(Window window)
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
                    await _service.AddProductAsync(Product, _currentUser.Id, Guid.NewGuid());
                }
                else
                {
                    await _service.UpdateProductAsync(Product.Id, Product, _expectedRowVersion!, _currentUser.Id, Guid.NewGuid());
                }
                window.DialogResult = true;
                window.Close();
            }
            catch (DatabaseWriteConflictException)
            {
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
        private string Serialize(Product p)
        {
            return System.Text.Json.JsonSerializer.Serialize(new { p.Id, p.ProductCode, p.DisplayName, p.CategoryId, p.BrandId, p.DefaultUnitId, p.DefaultPrice, p.IsActive });
        }
    }
}
