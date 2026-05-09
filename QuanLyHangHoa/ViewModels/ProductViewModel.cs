using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Data;
using System.Text.Json;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly ProductService _service;
        private readonly ReferenceDataService _refDataService;
        private readonly AppUser _currentUser;

        [ObservableProperty] private bool _canManage;
        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private ObservableCollection<Brand> _brands = new();
        [ObservableProperty] private ObservableCollection<Unit> _units = new();

        // Search Filters
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private string _searchStatus = "Tất cả";
        [ObservableProperty] private string _searchSerial = "Tất cả";
        [ObservableProperty] private string _searchWarranty = string.Empty;
        [ObservableProperty] private Category? _selectedCategoryFilter;
        [ObservableProperty] private Brand? _selectedBrandFilter;
        [ObservableProperty] private string _searchPriceMin = string.Empty;
        [ObservableProperty] private string _searchPriceMax = string.Empty;

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchStatusChanged(string value) => LoadData();
        partial void OnSearchSerialChanged(string value) => LoadData();
        partial void OnSearchWarrantyChanged(string value) => LoadData();
        partial void OnSelectedCategoryFilterChanged(Category? value) => LoadData();
        partial void OnSelectedBrandFilterChanged(Brand? value) => LoadData();
        partial void OnSearchPriceMinChanged(string value) => LoadData();
        partial void OnSearchPriceMaxChanged(string value) => LoadData();

        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng"];
        public ObservableCollection<string> SerialOptions { get; } = ["Tất cả", "Có serial", "Không serial"];

        [ObservableProperty] private int _lowStockCount;
        [ObservableProperty] private int _outOfStockCount;
        [ObservableProperty] private int _outOfStockActiveCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _inactiveCount;

        public ProductViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _currentUser = currentUser;
            _service = new ProductService(_contextFactory);
            _refDataService = new ReferenceDataService(_contextFactory);
            
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);

            var allCategories = _refDataService.GetAllCategories(false);
            Categories = new ObservableCollection<Category>(allCategories);
            Categories.Insert(0, new Category { Id = 0, DisplayName = "Tất cả danh mục" });

            var allBrands = _refDataService.GetAllBrands();
            Brands = new ObservableCollection<Brand>(allBrands);
            Brands.Insert(0, new Brand { Id = 0, DisplayName = "Tất cả thương hiệu" });
            
            Units = new ObservableCollection<Unit>(_refDataService.GetAllUnits());

            SelectedCategoryFilter = Categories.FirstOrDefault();
            SelectedBrandFilter = Brands.FirstOrDefault();
            
            LoadData();
        }

        private void LoadData()
        {
            var results = _service.GetAllProducts(onlyActive: false);

            // Apply Filters
            if (!string.IsNullOrWhiteSpace(SearchCode))
            {
                var term = SearchCode.ToLower();
                results = results.Where(p => p.ProductCode.ToLower().Contains(term)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(SearchName))
            {
                var term = SearchName.ToLower();
                results = results.Where(p => p.DisplayName.ToLower().Contains(term)).ToList();
            }

            if (SearchStatus != "Tất cả")
            {
                bool active = SearchStatus == "Hoạt động";
                results = results.Where(p => p.IsActive == active).ToList();
            }

            if (SelectedCategoryFilter != null && SelectedCategoryFilter.Id > 0)
            {
                results = results.Where(p => p.CategoryId == SelectedCategoryFilter.Id).ToList();
            }

            if (SelectedBrandFilter != null && SelectedBrandFilter.Id > 0)
            {
                results = results.Where(p => p.BrandId == SelectedBrandFilter.Id).ToList();
            }

            if (decimal.TryParse(SearchPriceMin, out decimal min))
            {
                results = results.Where(p => p.DefaultPrice >= min).ToList();
            }

            if (decimal.TryParse(SearchPriceMax, out decimal max))
            {
                results = results.Where(p => p.DefaultPrice <= max).ToList();
            }

            if (SearchSerial != "Tất cả")
            {
                bool tracked = SearchSerial == "Có serial";
                results = results.Where(p => p.IsSerialTracked == tracked).ToList();
            }

            if (int.TryParse(SearchWarranty, out int warranty))
            {
                results = results.Where(p => p.WarrantyPeriodMonths == warranty).ToList();
            }

            Products = new ObservableCollection<Product>(results);

            // Update Stats
            LowStockCount = results.Count(p => p.StockBalances.Sum(sb => sb.OnHandQuantity) > 0 && p.StockBalances.Sum(sb => sb.OnHandQuantity) <= 5);
            OutOfStockCount = results.Count(p => p.StockBalances.Sum(sb => sb.OnHandQuantity) <= 0);
            OutOfStockActiveCount = results.Count(p => p.IsActive && p.StockBalances.Sum(sb => sb.OnHandQuantity) <= 0);
            ActiveCount = results.Count(p => p.IsActive);
            InactiveCount = results.Count(p => !p.IsActive);
        }

        [RelayCommand]
        private void Search() => LoadData();

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchStatus = "Tất cả";
            SearchSerial = "Tất cả";
            SearchWarranty = string.Empty;
            SearchPriceMin = string.Empty;
            SearchPriceMax = string.Empty;
            SelectedCategoryFilter = Categories.FirstOrDefault();
            SelectedBrandFilter = Brands.FirstOrDefault();
            LoadData();
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddProductWindow()
        {
            var vm = new ProductEditViewModel(_contextFactory, _currentUser);
            var window = new ProductEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenEditProductWindow(Product? product)
        {
            if (product == null) return;
            var vm = new ProductEditViewModel(_contextFactory, _currentUser, product);
            var window = new ProductEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                LoadData();
            }
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void DeleteProduct(Product? product)
        {
            if (product == null) return;
            
            // 1. Kiểm tra phát sinh dữ liệu
            if (_service.HasTransactionHistory(product.Id))
            {
                MessageBox.Show($"Không thể xoá sản phẩm '{product.DisplayName}' vì đã có dữ liệu phát sinh (Hóa đơn, Nhập/Xuất kho).\n\nVui lòng chuyển sản phẩm sang 'Dừng' nếu không còn sử dụng.", 
                    "Không thể xoá", MessageBoxButton.OK, MessageBoxImage.Stop);
                return;
            }

            // 2. Xác nhận xoá (nếu không có lịch sử)
            var result = MessageBox.Show($"Sản phẩm '{product.DisplayName}' chưa có lịch sử giao dịch. Bạn có chắc chắn muốn xoá vĩnh viễn sản phẩm này khỏi hệ thống?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                try 
                {
                    _service.DeleteProduct(product.Id, _currentUser.Id);
                    LoadData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xoá sản phẩm: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }



        [RelayCommand]
        private void ExportToExcel()
        {
            if (Products == null || !Products.Any())
            {
                MessageBox.Show("Không có dữ liệu để xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"DanhSachSanPham_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Xuất danh sách sản phẩm"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Products");

                    // Headers
                    string[] headers = { "Mã sản phẩm", "Tên sản phẩm", "Danh mục", "Thương hiệu", "Serial", "Bảo hành (tháng)", "Đơn vị", "Giá bán", "Trạng thái" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#4A5568");
                        cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
                    }

                    // Data
                    int row = 2;
                    foreach (var p in Products)
                    {
                        worksheet.Cell(row, 1).Value = p.ProductCode;
                        worksheet.Cell(row, 2).Value = p.DisplayName;
                        worksheet.Cell(row, 3).Value = p.Category?.DisplayName ?? "N/A";
                        worksheet.Cell(row, 4).Value = p.Brand?.DisplayName ?? "N/A";
                        worksheet.Cell(row, 5).Value = p.IsSerialTracked ? "Có" : "-";
                        worksheet.Cell(row, 6).Value = p.WarrantyPeriodMonths;
                        worksheet.Cell(row, 7).Value = p.DefaultUnit?.DisplayName ?? "N/A";
                        worksheet.Cell(row, 8).Value = p.DefaultPrice;
                        worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
                        worksheet.Cell(row, 9).Value = p.IsActive ? "Hoạt động" : "Dừng";
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(saveFileDialog.FileName);
                    
                    MessageBox.Show($"Đã xuất {Products.Count} sản phẩm ra tệp Excel thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
