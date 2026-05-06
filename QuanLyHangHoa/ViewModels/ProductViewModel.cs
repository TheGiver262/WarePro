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

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductViewModel : ObservableObject
    {
        private readonly AppDbContext _db;
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
        [ObservableProperty] private Category? _selectedCategoryFilter;

        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng hoạt động"];

        [ObservableProperty] private int _lowStockCount;
        [ObservableProperty] private int _outOfStockCount;

        public ProductViewModel(AppDbContext db, AppUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
            _service = new ProductService(() => new AppDbContext());
            _refDataService = new ReferenceDataService();
            
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);

            Categories = new ObservableCollection<Category>(_refDataService.GetAllCategories(false));
            Brands = new ObservableCollection<Brand>(_refDataService.GetAllBrands());
            Units = new ObservableCollection<Unit>(_refDataService.GetAllUnits());

            LoadData();
        }

        private void LoadData()
        {
            var results = _service.GetAllProducts(onlyActive: false); // We handle filtering in VM

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

            if (SelectedCategoryFilter != null)
            {
                results = results.Where(p => p.CategoryId == SelectedCategoryFilter.Id).ToList();
            }

            Products = new ObservableCollection<Product>(results);

            // Update Stats
            LowStockCount = results.Count(p => p.StockBalances.Sum(sb => sb.OnHandQuantity) > 0 && p.StockBalances.Sum(sb => sb.OnHandQuantity) <= 5);
            OutOfStockCount = results.Count(p => p.StockBalances.Sum(sb => sb.OnHandQuantity) <= 0);
        }

        [RelayCommand]
        private void Search() => LoadData();

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddProductWindow()
        {
            var vm = new ProductEditViewModel();
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
            var vm = new ProductEditViewModel(product);
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
            
            var result = MessageBox.Show($"Bạn có chắc chắn muốn xoá sản phẩm '{product.DisplayName}'?", 
                "Xác nhận xoá", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                _service.DeactivateProduct(product.Id);
                LoadData();
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
                    string[] headers = { "Mã sản phẩm", "Tên sản phẩm", "Danh mục", "Thương hiệu", "Đơn vị", "Giá bán", "Tồn kho", "Trạng thái" };
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
                        worksheet.Cell(row, 5).Value = p.DefaultUnit?.DisplayName ?? "N/A";
                        worksheet.Cell(row, 6).Value = p.DefaultPrice;
                        worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                        worksheet.Cell(row, 7).Value = p.StockBalances.Sum(sb => sb.OnHandQuantity);
                        worksheet.Cell(row, 8).Value = p.IsActive ? "Hoạt động" : "Dừng hoạt động";
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

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchStatusChanged(string value) => LoadData();
        partial void OnSelectedCategoryFilterChanged(Category? value) => LoadData();
    }
}
