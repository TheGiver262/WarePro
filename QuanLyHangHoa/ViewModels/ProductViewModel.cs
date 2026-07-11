using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Views;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductViewModel : ObservableObject, IRefreshable
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly ProductService _service;
        private readonly ReferenceDataService _refDataService;
        private readonly AppUser _currentUser;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;
        private bool _isUpdatingFilters;
        private bool _reloadRequested;
        private CancellationTokenSource? _filterDebounceCts;
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
        [ObservableProperty] private bool _isAdvancedFilterOpen;

        [RelayCommand]
        private void ToggleAdvancedFilter() => IsAdvancedFilterOpen = !IsAdvancedFilterOpen;

        partial void OnSearchCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSearchNameChanged(string value) => ScheduleFilterReload();
        partial void OnSearchStatusChanged(string value) => ScheduleFilterReload();
        partial void OnSearchSerialChanged(string value) => ScheduleFilterReload();
        partial void OnSearchWarrantyChanged(string value) => ScheduleFilterReload();
        partial void OnSelectedCategoryFilterChanged(Category? value) => ScheduleFilterReload();
        partial void OnSelectedBrandFilterChanged(Brand? value) => ScheduleFilterReload();
        partial void OnSearchPriceMinChanged(string value) => ScheduleFilterReload();
        partial void OnSearchPriceMaxChanged(string value) => ScheduleFilterReload();

        public ObservableCollection<string> StatusOptions { get; } = ["Tất cả", "Hoạt động", "Dừng"];
        public ObservableCollection<string> SerialOptions { get; } = ["Tất cả", "Có serial", "Không serial"];

        [ObservableProperty] private int _lowStockCount;
        [ObservableProperty] private int _outOfStockCount;
        [ObservableProperty] private int _outOfStockActiveCount;
        [ObservableProperty] private int _activeCount;
        [ObservableProperty] private int _inactiveCount;
        [ObservableProperty] private int _totalCount;

        public ProductViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _currentUser = currentUser;
            _service = new ProductService(_contextFactory);
            _refDataService = new ReferenceDataService(_contextFactory);
            
            CanManage = AuthorizationService.CanPerform(_currentUser, PermissionAction.ManageMasterData);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var categoriesTask = Task.Run(() => _refDataService.GetAllCategories(false));
                var brandsTask = Task.Run(() => _refDataService.GetAllBrands());
                var unitsTask = Task.Run(() => _refDataService.GetAllUnits());

                await Task.WhenAll(categoriesTask, brandsTask, unitsTask);

                Categories = new ObservableCollection<Category>(await categoriesTask);
                Categories.Insert(0, new Category { Id = 0, DisplayName = "Tất cả danh mục" });

                Brands = new ObservableCollection<Brand>(await brandsTask);
                Brands.Insert(0, new Brand { Id = 0, DisplayName = "Tất cả thương hiệu" });
                Units = new ObservableCollection<Unit>(await unitsTask);

                SelectedCategoryFilter = Categories.FirstOrDefault();
                SelectedBrandFilter = Brands.FirstOrDefault();
                LoadData();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể tải dữ liệu sản phẩm: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadCounts()
        {
            _ = LoadCountsAsync();
        }

        public async Task LoadCountsAsync()
        {
            await Task.Run(() =>
            {
                using var db = _contextFactory();
                var statusCounts = db.Products
                    .GroupBy(p => p.IsActive)
                    .Select(g => new { IsActive = g.Key, Count = g.Count() })
                    .ToList();

                var active = statusCounts.FirstOrDefault(x => x.IsActive)?.Count ?? 0;
                var inactive = statusCounts.FirstOrDefault(x => !x.IsActive)?.Count ?? 0;
                var total = active + inactive;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    TotalCount = total;
                    ActiveCount = active;
                    InactiveCount = inactive;
                });
            });
        }
        private void LoadData()
        {
            LoadCounts();
            _ = ApplyFiltersAsync(true);
        }

        private void ApplyFilters()
        {
            if (_isLoading)
            {
                _reloadRequested = true;
                return;
            }

            _ = ApplyFiltersAsync(true);
        }

        private void ScheduleFilterReload()
        {
            if (!_isInitialized || _isUpdatingFilters)
            {
                return;
            }

            _filterDebounceCts?.Cancel();
            _filterDebounceCts?.Dispose();
            _filterDebounceCts = new CancellationTokenSource();
            _ = ReloadAfterDelayAsync(_filterDebounceCts.Token);
        }

        private async Task ReloadAfterDelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(300, cancellationToken);
                ApplyFilters();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ApplyFiltersAsync(bool reset)
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                if (reset)
                {
                    _skip = 0;
                    Products.Clear();
                }

                var searchCode = SearchCode;
                var searchName = SearchName;
                var searchStatus = SearchStatus;
                var searchSerial = SearchSerial;
                int? categoryId = SelectedCategoryFilter?.Id > 0 ? SelectedCategoryFilter.Id : null;
                int? brandId = SelectedBrandFilter?.Id > 0 ? SelectedBrandFilter.Id : null;
                decimal? priceMin = decimal.TryParse(SearchPriceMin, out decimal min) ? min : null;
                decimal? priceMax = decimal.TryParse(SearchPriceMax, out decimal max) ? max : null;
                int? warranty = int.TryParse(SearchWarranty, out int war) ? war : null;
                var skip = _skip;

                var list = await Task.Run(() => _service.GetProductsPaged(
                    searchCode, searchName, searchStatus, searchSerial, categoryId, brandId, priceMin, priceMax, warranty, skip, PageSize));

                foreach (var p in list)
                {
                    Products.Add(p);
                }
                _skip += list.Count;

                LowStockCount = Products.Count(product =>
                    product.StockBalances.Sum(balance => balance.OnHandQuantity) is > 0 and <= 5);
                OutOfStockCount = Products.Count(product =>
                    product.StockBalances.Sum(balance => balance.OnHandQuantity) <= 0);
                OutOfStockActiveCount = Products.Count(product =>
                    product.IsActive && product.StockBalances.Sum(balance => balance.OnHandQuantity) <= 0);
            }
            catch (Exception)
            {
                // Silence or handle
            }
            finally
            {
                _isLoading = false;
                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    ApplyFilters();
                }
            }
        }

        [RelayCommand]
        private async Task LoadMore()
        {
            await ApplyFiltersAsync(false);
        }
        [RelayCommand]
        private void Search() => ApplyFilters();

        [RelayCommand]
        private void Refresh()
        {
            _isUpdatingFilters = true;
            try
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
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            LoadData();
        }

        [RelayCommand(CanExecute = nameof(CanManage))]
        private void OpenAddProductWindow()
        {
            var vm = new ProductEditViewModel(_contextFactory, _currentUser);
            var window = new ProductEditWindow { DataContext = vm };
            if (window.ShowDialog() == true)
            {
                LoadCounts();
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
                LoadCounts();
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
                    LoadCounts();
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

        public void RefreshData()
        {
            LoadData();
        }
    }
}
