using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
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
    public partial class ProductSerialViewModel : ObservableObject, IRefreshable
    {
        private readonly Func<string, string, string, string, DateTime?, DateTime?, string, List<ProductSerial>> _serialLoader;
        private readonly Func<string, string, string, string, DateTime?, DateTime?, string, int, int, List<ProductSerial>> _serialPagedLoader;
        private readonly IProductSerialImportService _importService;
        private readonly Func<AppDbContext> _contextFactory;
        private readonly AppUser _currentUser;
        private bool _isInitialized;
        private int _skip = 0;
        private const int PageSize = 100;
        private bool _isLoading;
        private CancellationTokenSource? _filterDebounceCts;

        private bool _isUpdatingFilters;

        [ObservableProperty] private ObservableCollection<ProductSerial> _serials = new();
        [ObservableProperty] private ObservableCollection<string> _statuses = new();
        
        [ObservableProperty] private string _searchSerial = string.Empty;
        [ObservableProperty] private string _searchProduct = string.Empty;
        [ObservableProperty] private string _searchBrand = string.Empty;
        [ObservableProperty] private string _selectedStatus = "Tất cả trạng thái";

        // Advanced Filters
        [ObservableProperty] private bool _isAdvancedFilterOpen;
        [ObservableProperty] private DateTime? _searchFromDate;
        [ObservableProperty] private DateTime? _searchToDate;
        [ObservableProperty] private string _searchNote = string.Empty;

        partial void OnSearchSerialChanged(string value) => ScheduleFilterReload();
        partial void OnSearchProductChanged(string value) => ScheduleFilterReload();
        partial void OnSearchBrandChanged(string value) => ScheduleFilterReload();
        partial void OnSelectedStatusChanged(string value) => ScheduleFilterReload();
        partial void OnSearchFromDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnSearchToDateChanged(DateTime? value) => ScheduleFilterReload();
        partial void OnSearchNoteChanged(string value) => ScheduleFilterReload();

        [ObservableProperty] private ProductSerial? _selectedSerial;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private bool _canManage = true; 

        // Statistics for Footer
        [ObservableProperty] private int _inStockCount;
        [ObservableProperty] private int _soldCount;
        [ObservableProperty] private int _scrappedCount;
        [ObservableProperty] private int _totalCount;
        public ProductSerialViewModel(Func<AppDbContext> contextFactory, AppUser currentUser)
            : this(
                contextFactory, 
                new ProductSerialService(contextFactory).SearchSerials, 
                new ProductSerialService(contextFactory).SearchSerialsPaged,
                new ProductSerialImportService(contextFactory), 
                currentUser)
        {
        }

        public ProductSerialViewModel(
            Func<AppDbContext> contextFactory, 
            Func<string, string, string, string, DateTime?, DateTime?, string, List<ProductSerial>> serialLoader, 
            IProductSerialImportService importService, 
            AppUser currentUser)
            : this(
                contextFactory, 
                serialLoader, 
                new ProductSerialService(contextFactory).SearchSerialsPaged, 
                importService, 
                currentUser)
        {
        }

        public ProductSerialViewModel(
            Func<AppDbContext> contextFactory, 
            Func<string, string, string, string, DateTime?, DateTime?, string, List<ProductSerial>> serialLoader, 
            Func<string, string, string, string, DateTime?, DateTime?, string, int, int, List<ProductSerial>> serialPagedLoader,
            IProductSerialImportService importService, 
            AppUser currentUser)
        {
            _contextFactory = contextFactory;
            _serialLoader = serialLoader;
            _serialPagedLoader = serialPagedLoader;
            _importService = importService;
            _currentUser = currentUser;
            
            Statuses = new ObservableCollection<string> 
            { 
                "Tất cả trạng thái", 
                "Trong kho", 
                "Đã bán", 
                "Đã đặt", 
                "Đang bảo hành", 
                "Lỗi bảo hành", 
                "Đã trả hàng", 
                "Trả lại NCC",
                "Đã đổi mới", 
                "Đã thanh lý",
                "Dừng" 
            };

            LoadCounts();
            LoadSerials();
            _isInitialized = true;
        }

        [RelayCommand]
        private void ToggleAdvancedFilter()
        {
            IsAdvancedFilterOpen = !IsAdvancedFilterOpen;
            if (!IsAdvancedFilterOpen)
            {
                SearchFromDate = null;
                SearchToDate = null;
                SearchNote = string.Empty;
            }
        }

        [RelayCommand]
        private void Search() => LoadSerials();

        [RelayCommand]
        private void Refresh()
        {
            _isUpdatingFilters = true;
            try
            {
                SearchSerial = string.Empty;
                SearchProduct = string.Empty;
                SearchBrand = string.Empty;
                SelectedStatus = "Tất cả trạng thái";
                SearchFromDate = null;
                SearchToDate = null;
                SearchNote = string.Empty;
            }
            finally
            {
                _isUpdatingFilters = false;
            }
            LoadCounts();
            LoadSerials();
        }

        [RelayCommand]
        // chụp collection hiện tại và tạo workbook ở worker thread để không khóa giao diện
        private async Task ExportToExcel()
        {
            if (Serials == null || !Serials.Any())
            {
                StatusMessage = "Không có dữ liệu để xuất.";
                return;
            }

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = $"DanhSachSerial_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "Đang xuất dữ liệu ra Excel...";
                    await Task.Run(() =>
                    {
                        using (var workbook = new ClosedXML.Excel.XLWorkbook())
                        {
                            var worksheet = workbook.Worksheets.Add("Danh sách Serial");

                            // Headers
                            worksheet.Cell(1, 1).Value = "STT";
                            worksheet.Cell(1, 2).Value = "Số Serial";
                            worksheet.Cell(1, 3).Value = "Tên sản phẩm";
                            worksheet.Cell(1, 4).Value = "Mã sản phẩm";
                            worksheet.Cell(1, 5).Value = "Thương hiệu";
                            worksheet.Cell(1, 6).Value = "Trạng thái";
                            worksheet.Cell(1, 7).Value = "Kho hiện tại";
                            worksheet.Cell(1, 8).Value = "Ngày nhập";
                            worksheet.Cell(1, 9).Value = "Mã phiếu nhập";
                            worksheet.Cell(1, 10).Value = "Ghi chú";

                            // Style headers
                            var headerRange = worksheet.Range(1, 1, 1, 10);
                            headerRange.Style.Font.Bold = true;
                            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#3B82F6");
                            headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

                            // Data
                            int row = 2;
                            foreach (var item in Serials)
                            {
                                worksheet.Cell(row, 1).Value = row - 1;
                                worksheet.Cell(row, 2).Value = item.SerialNumber;
                                worksheet.Cell(row, 3).Value = item.Product?.DisplayName;
                                worksheet.Cell(row, 4).Value = item.Product?.ProductCode;
                                worksheet.Cell(row, 5).Value = item.Product?.Brand?.DisplayName;
                                worksheet.Cell(row, 6).Value = GetStatusDisplay(item.CurrentStatus);
                                worksheet.Cell(row, 7).Value = item.CurrentWarehouse?.DisplayName;
                                worksheet.Cell(row, 8).Value = item.LastStockInLine?.StockIn?.CreatedAt.ToString("dd/MM/yyyy");
                                worksheet.Cell(row, 9).Value = item.LastStockInLine?.StockIn?.DocumentCode;
                                worksheet.Cell(row, 10).Value = item.Note;
                                row++;
                            }

                            worksheet.Columns().AdjustToContents();
                            workbook.SaveAs(saveFileDialog.FileName);
                        }
                    });

                    StatusMessage = $"Đã xuất file thành công: {Path.GetFileName(saveFileDialog.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Lỗi khi xuất file: {ex.Message}";
                }
            }
        }

        private string GetStatusDisplay(string status)
        {
            if (string.Equals(status, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase))
                return "Trả lại NCC";

            return status switch
            {
                "InStock" => "Trong kho",
                "Sold" => "Đã bán",
                "Reserved" => "Đã đặt",
                "InWarrantyProcess" => "Đang bảo hành",
                "WarrantyDefective" => "Lỗi bảo hành",
                "Returned" => "Đã trả hàng",
                "ReturnedToManufacturer" => "Trả lại NCC",
                "Scrapped" => "Đã thanh lý",
                "Replaced" => "Đã đổi mới",
                "Inactive" => "Dừng",
                _ => status
            };
        }

        [RelayCommand]
        private void ClearSearch()
        {
            Refresh();
        }

        [RelayCommand]
        // import dùng file seed chuẩn, chỉ reload số đếm/danh sách khi có ít nhất một dòng thành công
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
                var result = await _importService.ImportFromExcelAsync(excelPath, _currentUser.Id);
                
                StatusMessage = result.Message;
                if (result.SuccessCount > 0)
                {
                    LoadCounts();
                    LoadSerials();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi khi nạp dữ liệu: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewSerialDetail(ProductSerial serial)
        {
            if (serial == null) return;
            StatusMessage = $"Đang hiển thị chi tiết serial: {serial.SerialNumber}";
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => {
                    try {
                        var detailWindow = new QuanLyHangHoa.Views.ProductSerialDetailView(serial);
                        detailWindow.Owner = System.Windows.Application.Current.MainWindow;
                        detailWindow.ShowDialog();
                    } catch (Exception ex) {
                        StatusMessage = $"Lỗi mở cửa sổ chi tiết: {ex.Message}";
                    }
                });
            }
        }

        [RelayCommand]
        private void EditSerial(ProductSerial serial)
        {
            if (serial == null) return;
            
            if (System.Windows.Application.Current != null)
            {
                var editWindow = new QuanLyHangHoa.Views.ProductSerialEditView(_contextFactory, serial, _currentUser.Id);
                editWindow.Owner = System.Windows.Application.Current.MainWindow;
                if (editWindow.ShowDialog() == true)
                {
                    StatusMessage = $"Đã cập nhật serial {serial.SerialNumber}";
                    LoadCounts();
                    LoadSerials();
                }
            }
        }

        public void LoadCounts()
        {
            _ = LoadCountsAsync();
        }

        // group status trong một query, sau đó marshal kết quả về dispatcher trước khi sửa property bind UI
        public async Task LoadCountsAsync()
        {
            await Task.Run(() =>
            {
                using var db = _contextFactory();
                var statusCounts = db.ProductSerials
                    .GroupBy(s => s.CurrentStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToList();

                var inStock = statusCounts.FirstOrDefault(x => x.Status == "InStock")?.Count ?? 0;
                var sold = statusCounts.FirstOrDefault(x => x.Status == "Sold")?.Count ?? 0;
                var scrapped = statusCounts.FirstOrDefault(x => x.Status == "Scrapped")?.Count ?? 0;
                var total = statusCounts.Sum(x => x.Count);

                if (Application.Current != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TotalCount = total;
                        InStockCount = inStock;
                        SoldCount = sold;
                        ScrappedCount = scrapped;
                    });
                }
                else
                {
                    TotalCount = total;
                    InStockCount = inStock;
                    SoldCount = sold;
                    ScrappedCount = scrapped;
                }
            });
        }

        private CancellationTokenSource? _cts;

        // debounce filter; lượt mới hủy token chờ của lượt cũ
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
                LoadSerials();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void LoadSerials()
        {
            _ = LoadSerialsAsync(true);
        }

        // reset hủy request cũ, chụp filter/skip rồi tải trang và tổng số song song
        private async Task LoadSerialsAsync(bool reset)
        {
            if (reset)
            {
                _cts?.Cancel();
                _cts = new System.Threading.CancellationTokenSource();
            }

            var token = _cts?.Token ?? CancellationToken.None;

            if (_isLoading && !reset)
            {
                return;
            }

            _isLoading = true;
            try
            {
                if (reset)
                {
                    _skip = 0;
                    Serials.Clear();
                }

                string dbStatus = SelectedStatus switch
                {
                    "Trong kho" => "InStock",
                    "Đã bán" => "Sold",
                    "Đã đặt" => "Reserved",
                    "Đang bảo hành" => "InWarrantyProcess",
                    "Lỗi bảo hành" => "WarrantyDefective",
                    "Đã trả hàng" => "Returned",
                    "Trả lại NCC" => "ReturnedToManufacturer",
                    "Đã đổi mới" => "Replaced",
                    "Đã thanh lý" => "Scrapped",
                    "Dừng" => "Inactive",
                    _ => "All"
                };

                var searchSerial = SearchSerial;
                var searchProduct = SearchProduct;
                var searchBrand = SearchBrand;
                var searchFromDate = SearchFromDate;
                var searchToDate = SearchToDate;
                var searchNote = SearchNote;
                var skip = _skip;

                // token được kiểm tra cả trước query và trước khi thêm từng item để kết quả cũ không chen vào collection mới
                var dataTask = Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    return _serialPagedLoader(
                        searchSerial, searchProduct, searchBrand, dbStatus, searchFromDate, searchToDate, searchNote, skip, PageSize);
                }, token);

                int totalFilteredCount = 0;
                bool isTestEnv = false;
                try
                {
                    isTestEnv = _contextFactory() == null;
                }
                catch
                {
                    isTestEnv = true;
                }

                Task<int>? countTask = null;
                if (!isTestEnv)
                {
                    countTask = Task.Run(() =>
                    {
                        token.ThrowIfCancellationRequested();
                        return new ProductSerialService(_contextFactory).GetSerialsCount(
                            searchSerial, searchProduct, searchBrand, dbStatus, searchFromDate, searchToDate, searchNote);
                    }, token);
                }

                var data = await dataTask;
                token.ThrowIfCancellationRequested();

                // test delegate không có database nên dùng số dòng trả về; runtime chạy query count thật
                if (isTestEnv)
                {
                    totalFilteredCount = data.Count;
                }
                else
                {
                    totalFilteredCount = await countTask!;
                }

                token.ThrowIfCancellationRequested();

                foreach (var item in data)
                {
                    token.ThrowIfCancellationRequested();
                    Serials.Add(item);
                }
                _skip += data.Count;

                if (Serials.Count >= totalFilteredCount)
                {
                    StatusMessage = $"Đã tải toàn bộ {Serials.Count} serial.";
                }
                else
                {
                    StatusMessage = $"Đang hiển thị {Serials.Count} / {totalFilteredCount} serial.";
                }
            }
            catch (OperationCanceledException)
            {
                // Bị hủy bởi cuộc gọi nạp mới hơn, bỏ qua an toàn
            }
            catch (Exception ex)
            {
                StatusMessage = $"Lỗi tải dữ liệu: {ex.Message}";
                CrashLogger.Write(ex, "Product serial loading");
            }
            finally
            {
                _isLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadMore()
        {
            await LoadSerialsAsync(false);
        }

        public void RefreshData()
        {
            LoadSerials();
        }
    }
}
