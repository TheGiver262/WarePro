using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class InventoryViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading = false;
        private bool _isInitialized = false;
        private bool _isUpdatingFilters;
        private bool _reloadRequested;
        private CancellationTokenSource? _filterDebounceCts;

        [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private int _lowStockCount;
        [ObservableProperty] private decimal _totalInventoryValue;
        [ObservableProperty] private string? _loadErrorMessage;

        [ObservableProperty] private ObservableCollection<Category> _categories = new();
        [ObservableProperty] private Category? _selectedCategoryFilter;
        [ObservableProperty] private string _searchStatus = "Tất cả";
        [ObservableProperty] private bool _isAdvancedFilterOpen;

        public ObservableCollection<string> StatusOptions { get; } = new() { "Tất cả", "Còn hàng", "Sắp hết", "Hết hàng" };

        private readonly Func<Data.AppDbContext> _contextFactory;

        public InventoryViewModel(Func<Data.AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _productService = new ProductService(contextFactory);
            _refDataService = new ReferenceDataService(contextFactory);

            var allCategories = _refDataService.GetAllCategories(false);
            Categories = new ObservableCollection<Category>(allCategories);
            Categories.Insert(0, new Category { Id = 0, DisplayName = "Tất cả danh mục" });
            SelectedCategoryFilter = Categories.FirstOrDefault();

            LoadData();
            _isInitialized = true;
        }

        [RelayCommand]
        private void ToggleAdvancedFilter() => IsAdvancedFilterOpen = !IsAdvancedFilterOpen;

        partial void OnSearchCodeChanged(string value) => ScheduleFilterReload();
        partial void OnSearchNameChanged(string value) => ScheduleFilterReload();
        partial void OnSearchStatusChanged(string value) => ScheduleFilterReload();
        partial void OnSelectedCategoryFilterChanged(Category? value) => ScheduleFilterReload();

        private void LoadData()
        {
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

        // mỗi thay đổi filter hủy lượt chờ cũ; chỉ truy vấn sau 300 ms không còn thao tác
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

        // chụp toàn bộ filter và skip trước await để một lần tải dùng một bộ điều kiện nhất quán
        private async Task ApplyFiltersAsync(bool reset)
        {
            if (_isLoading) return;
            _isLoading = true;
            try
            {
                var searchCode = SearchCode;
                var searchName = SearchName;
                int? categoryId = SelectedCategoryFilter?.Id > 0 ? SelectedCategoryFilter.Id : null;
                var searchStatus = SearchStatus;
                var skip = reset ? 0 : _skip;

                // danh sách và thống kê chạy song song nhưng dùng context riêng do service tự tạo
                var listTask = Task.Run(() => _productService.GetInventoryProductsPaged(
                    searchCode, searchName, categoryId, searchStatus, skip, PageSize));
                var statsTask = Task.Run(() => _productService.GetInventoryStats(
                    searchCode, searchName, categoryId, searchStatus));

                await Task.WhenAll(listTask, statsTask);
                var list = await listTask;
                var stats = await statsTask;

                if (reset)
                {
                    InventoryItems.Clear();
                }
                foreach (var p in list)
                {
                    InventoryItems.Add(p);
                }
                _skip = skip + list.Count;

                LowStockCount = stats.lowStockCount;
                TotalInventoryValue = stats.totalValue;
                LoadErrorMessage = null;
            }
            catch (Exception ex) when (IsCancellation(ex))
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                LoadErrorMessage = DatabaseWriteUi.TechnicalErrorMessage;
            }
            finally
            {
                _isLoading = false;
                // filter đổi trong lúc đang tải được gom thành đúng một lượt reset tiếp theo
                if (_reloadRequested)
                {
                    _reloadRequested = false;
                    ApplyFilters();
                }
            }
        }

        private static bool IsCancellation(Exception exception) =>
            exception is OperationCanceledException ||
            exception is AggregateException aggregate &&
            aggregate.Flatten().InnerExceptions.All(inner => inner is OperationCanceledException);

        [RelayCommand]
        private async Task LoadMore()
        {
            await ApplyFiltersAsync(false);
        }

        [RelayCommand]
        private void Search()
        {
            ApplyFilters();
        }

        [RelayCommand]
        // _isUpdatingFilters ngăn từng property reset tự tạo nhiều query debounce
        private void Refresh()
        {
            _isUpdatingFilters = true;
            try
            {
                SearchCode = string.Empty;
                SearchName = string.Empty;
                SearchStatus = "Tất cả";
                SelectedCategoryFilter = Categories.FirstOrDefault();
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            ApplyFilters();
        }

        [RelayCommand]
        // xuất snapshot đang hiển thị; không truy vấn lại nên file khớp đúng bộ lọc người dùng đang xem
        private void Export()
        {
            if (InventoryItems == null || !InventoryItems.Any())
            {
                MessageBox.Show("Không có dữ liệu tồn kho để xuất.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                FileName = $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                Title = "Xuất báo cáo tồn kho"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using var workbook = new ClosedXML.Excel.XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Inventory");

                    // Headers
                    string[] headers = { "Mã sản phẩm", "Tên sản phẩm", "Danh mục", "Đơn vị", "Số lượng tồn", "Tổng giá trị" };
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
                    foreach (var p in InventoryItems)
                    {
                        worksheet.Cell(row, 1).Value = p.ProductCode;
                        worksheet.Cell(row, 2).Value = p.ProductName;
                        worksheet.Cell(row, 3).Value = p.CategoryName;
                        worksheet.Cell(row, 4).Value = p.UnitName;
                        
                        worksheet.Cell(row, 5).Value = p.StockQuantity;
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                        
                        worksheet.Cell(row, 6).Value = p.TotalValue;
                        worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();
                    workbook.SaveAs(saveFileDialog.FileName);
                    
                    MessageBox.Show($"Đã xuất {InventoryItems.Count} mặt hàng tồn kho ra tệp Excel thành công.", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
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
