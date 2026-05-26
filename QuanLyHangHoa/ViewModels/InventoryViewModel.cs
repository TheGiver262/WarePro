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
    public partial class InventoryViewModel : ObservableObject, IRefreshable
    {
        private readonly ProductService _productService;
        private readonly ReferenceDataService _refDataService;

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
        }

        [RelayCommand]
        private void ToggleAdvancedFilter() => IsAdvancedFilterOpen = !IsAdvancedFilterOpen;

        partial void OnSearchCodeChanged(string value) => LoadData();
        partial void OnSearchNameChanged(string value) => LoadData();
        partial void OnSearchStatusChanged(string value) => LoadData();
        partial void OnSelectedCategoryFilterChanged(Category? value) => LoadData();

        [RelayCommand]
        private void LoadData()
        {
            var results = _productService.GetAllProducts();
            if (!string.IsNullOrWhiteSpace(SearchCode))
            {
                var term = SearchCode.ToLower().Trim();
                results = results.Where(p => p.ProductCode.ToLower().Contains(term)).ToList();
            }
            if (!string.IsNullOrWhiteSpace(SearchName))
            {
                var term = SearchName.ToLower().Trim();
                results = results.Where(p => p.DisplayName.ToLower().Contains(term)).ToList();
            }
            if (SelectedCategoryFilter != null && SelectedCategoryFilter.Id > 0)
            {
                results = results.Where(p => p.CategoryId == SelectedCategoryFilter.Id).ToList();
            }
            if (SearchStatus != "Tất cả")
            {
                if (SearchStatus == "Còn hàng")
                {
                    results = results.Where(p => p.StockQuantity > 0).ToList();
                }
                else if (SearchStatus == "Sắp hết")
                {
                    results = results.Where(p => p.IsLowStock).ToList();
                }
                else if (SearchStatus == "Hết hàng")
                {
                    results = results.Where(p => p.StockQuantity <= 0).ToList();
                }
            }

            InventoryItems = new ObservableCollection<Product>(results);
            
            // Tính toán thống kê động
            LowStockCount = results.Count(p => p.IsLowStock);
            TotalInventoryValue = results.Sum(p => p.TotalValue);
        }

        [RelayCommand]
        private void Search()
        {
            LoadData();
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchCode = string.Empty;
            SearchName = string.Empty;
            SearchStatus = "Tất cả";
            SelectedCategoryFilter = Categories.FirstOrDefault();
            LoadData();
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
