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
    public partial class InventoryViewModel : ObservableObject
    {
        private readonly ProductService _productService;

        [ObservableProperty] private ObservableCollection<Product> _inventoryItems = new();
        [ObservableProperty] private string _searchText = string.Empty;

        private readonly Func<Data.AppDbContext> _contextFactory;

        public InventoryViewModel(Func<Data.AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _productService = new ProductService(contextFactory);
            LoadData();
        }

        [RelayCommand]
        private void LoadData()
        {
            var results = _productService.GetAllProducts();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.ToLower();
                results = results.Where(p => 
                    p.DisplayName.ToLower().Contains(term) || 
                    p.ProductCode.ToLower().Contains(term)).ToList();
            }
            InventoryItems = new ObservableCollection<Product>(results);
        }

        [RelayCommand]
        private void Search()
        {
            LoadData();
        }

        [RelayCommand]
        private void Refresh()
        {
            SearchText = string.Empty;
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
    }
}
