using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
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

        [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
        [ObservableProperty] private string _searchCode = string.Empty;
        [ObservableProperty] private string _searchName = string.Empty;
        [ObservableProperty] private int _lowStockCount;
        [ObservableProperty] private decimal _totalInventoryValue;

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

        partial void OnSearchCodeChanged(string value) { if (_isInitialized) ApplyFilters(); }
        partial void OnSearchNameChanged(string value) { if (_isInitialized) ApplyFilters(); }
        partial void OnSearchStatusChanged(string value) { if (_isInitialized) ApplyFilters(); }
        partial void OnSelectedCategoryFilterChanged(Category? value) { if (_isInitialized) ApplyFilters(); }

        private void LoadData()
        {
            _ = ApplyFiltersAsync(true);
        }

        private void ApplyFilters()
        {
            _ = ApplyFiltersAsync(true);
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
                    InventoryItems.Clear();
                }

                int? catId = SelectedCategoryFilter?.Id > 0 ? SelectedCategoryFilter.Id : null;

                var list = await Task.Run(() => _productService.GetInventoryProductsPaged(
                    SearchCode, SearchName, catId, SearchStatus, _skip, PageSize));

                foreach (var p in list)
                {
                    InventoryItems.Add(p);
                }
                _skip += list.Count;

                // Tính toán thống kê động ngầm qua task chạy nền
                await Task.Run(() =>
                {
                    var stats = _productService.GetInventoryStats(
                        SearchCode, SearchName, catId, SearchStatus);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LowStockCount = stats.lowStockCount;
                        TotalInventoryValue = stats.totalValue;
                    });
                });
            }
            catch (Exception)
            {
                // Silence or handle
            }
            finally
            {
                _isLoading = false;
            }
        }

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
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchStatus = "Tất cả";
            SelectedCategoryFilter = Categories.FirstOrDefault();
            ApplyFilters();
        }

        [RelayCommand]
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
