using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ProductSerialViewModel : ObservableObject
    {
        private readonly Func<string, string, List<ProductSerial>> _serialLoader;
        private readonly IProductSerialImportService _importService;

        [ObservableProperty] private ObservableCollection<ProductSerial> _serials = new();
        [ObservableProperty] private ObservableCollection<string> _statuses = new();
        [ObservableProperty] private string _searchText = string.Empty;
        [ObservableProperty] private string _selectedStatus = "Tất cả trạng thái";

        partial void OnSearchTextChanged(string value) => LoadSerials();
        partial void OnSelectedStatusChanged(string value) => LoadSerials();
        [ObservableProperty] private ProductSerial? _selectedSerial;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _canManage = true; 

        public ProductSerialViewModel()
            : this(new ProductSerialService().SearchSerials, new ProductSerialImportService(new AppDbContext()))
        {
        }

        public ProductSerialViewModel(Func<string, string, List<ProductSerial>> serialLoader, IProductSerialImportService importService)
        {
            _serialLoader = serialLoader;
            _importService = importService;
            Statuses = new ObservableCollection<string> { "Tất cả trạng thái", "Trong kho", "Đã bán", "Đã đặt" };

            LoadSerials();
            
            // Tự động nạp dữ liệu nếu bảng trống
            if (Serials.Count == 0)
            {
                _ = Import(); 
            }
        }

        [RelayCommand]
        private void Search() => LoadSerials();

        [RelayCommand]
        private void Refresh()
        {
            SearchText = string.Empty;
            SelectedStatus = "Tất cả trạng thái";
            LoadSerials();
        }

        [RelayCommand]
        private async Task ExportToExcel()
        {
            // Implementation of export logic
            await Task.Delay(100); 
            StatusMessage = "Tính năng xuất Excel đang được cập nhật.";
        }

        [RelayCommand]
        private void ClearSearch()
        {
            Refresh();
        }

        [RelayCommand]
        private async Task Import()
        {
            try 
            {
                string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "WarePro_Export_5-5-2026.xlsx");
                if (!File.Exists(excelPath))
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var projectDir = Directory.GetParent(baseDir)?.Parent?.Parent?.FullName;
                    if (projectDir != null)
                        excelPath = Path.Combine(projectDir, "Database", "WarePro_Export_5-5-2026.xlsx");
                }

                if (!File.Exists(excelPath))
                {
                    StatusMessage = "Không tìm thấy file dữ liệu Excel để nạp tự động.";
                    return;
                }

                StatusMessage = "Đang xử lý dữ liệu từ Excel...";
                var result = await _importService.ImportFromExcelAsync(excelPath);
                
                StatusMessage = result.Message;
                if (result.SuccessCount > 0)
                {
                    LoadSerials();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi khi nạp dữ liệu: {ex.Message}";
            }
        }

        private void LoadSerials()
        {
            string dbStatus = SelectedStatus switch
            {
                "Trong kho" => "InStock",
                "Đã bán" => "Sold",
                "Đã đặt" => "Reserved",
                _ => "All"
            };

            var data = _serialLoader(SearchText, dbStatus);
            Serials = new ObservableCollection<ProductSerial>(data);
            
            if (string.IsNullOrEmpty(StatusMessage) || StatusMessage.Contains("Tìm thấy"))
                StatusMessage = $"Tìm thấy {Serials.Count} serial.";
        }
    }
}
