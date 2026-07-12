using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ReportViewModel : ObservableObject
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly ReportTraceService _traceService;
        private CancellationTokenSource? _refreshCts;
        private CancellationTokenSource? _searchDebounceCts;
        private bool _isInitialized;
        // --- CHUNG ---
        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-30);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private int _activeTabIndex = 0;

        // --- TAB 1: DOANH THU & LỢI NHUẬN ---
        [ObservableProperty] private decimal _totalRevenue = 0;
        [ObservableProperty] private decimal _totalProfit = 0;
        [ObservableProperty] private decimal _totalCost = 0;
        [ObservableProperty] private ObservableCollection<DailyReportItem> _dailyReports = new();
        [ObservableProperty] private ISeries[] _revenueExpenseSeries = Array.Empty<ISeries>();
        [ObservableProperty] private Axis[] _revenueExpenseXAxes = Array.Empty<Axis>();

        // --- TAB 2: XUẤT NHẬP TỒN TỔNG HỢP ---
        [ObservableProperty] private ObservableCollection<StockInOutTonReportItem> _stockInOutTonReports = new();
        [ObservableProperty] private string _searchProductText = string.Empty;
        [ObservableProperty] private Category? _selectedCategory;
        [ObservableProperty] private ObservableCollection<Category> _categories = new();

        // --- TAB 3: SỔ KHO / THẺ KHO CHI TIẾT ---
        [ObservableProperty] private Product? _selectedProduct;
        [ObservableProperty] private ObservableCollection<Product> _products = new();
        [ObservableProperty] private ObservableCollection<StockLedgerReportItem> _ledgerReports = new();
        [ObservableProperty] private decimal _ledgerStartQty = 0;
        [ObservableProperty] private decimal _ledgerEndQty = 0;

        // --- TAB 4: TRUY VẾT SERIAL ---
        [ObservableProperty] private string _searchSerialText = string.Empty;
        [ObservableProperty] private string _serialProductText = string.Empty;
        [ObservableProperty] private string _serialDocumentText = string.Empty;
        [ObservableProperty] private string _serialPartnerText = string.Empty;
        [ObservableProperty] private string? _selectedSerialStatus = "All";
        [ObservableProperty] private ObservableCollection<string> _serialStatuses = new(new[] { "All", "InStock", "Sold", "Transferred", "Reserved", "Warranty" });
        [ObservableProperty] private ObservableCollection<SerialTraceReportItem> _serialTraceReports = new();

        public ReportViewModel() : this(() => new AppDbContext())
        {
        }

        public ReportViewModel(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _traceService = new ReportTraceService(contextFactory);
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadFilterDataAsync();
            _isInitialized = true;
            await Refresh();
        }

        partial void OnActiveTabIndexChanged(int value)
        {
            if (_isInitialized)
            {
                _ = Refresh();
            }
        }

        partial void OnSelectedCategoryChanged(Category? value)
        {
            if (_isInitialized)
            {
                _ = Refresh();
            }
        }

        partial void OnSearchProductTextChanged(string value)
        {
            if (!_isInitialized)
            {
                return;
            }

            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();
            _searchDebounceCts = new CancellationTokenSource();
            _ = RefreshAfterDelayAsync(_searchDebounceCts.Token);
        }

        private async Task RefreshAfterDelayAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(300, cancellationToken);
                await Refresh();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task LoadFilterDataAsync()
        {
            try
            {
                using var db = _contextFactory();
                var activeCategories = await db.Categories
                    .AsNoTracking()
                    .Where(category => category.IsActive)
                    .OrderBy(category => category.DisplayName)
                    .ToListAsync();
                Categories = new ObservableCollection<Category>(activeCategories);
                Categories.Insert(0, new Category { Id = 0, DisplayName = "Tất cả danh mục" });
                SelectedCategory = Categories.FirstOrDefault();

                var activeProducts = await db.Products
                    .AsNoTracking()
                    .Where(product => product.IsActive)
                    .OrderBy(product => product.DisplayName)
                    .ToListAsync();
                Products = new ObservableCollection<Product>(activeProducts);
                SelectedProduct = activeProducts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi nạp danh sách bộ lọc: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task Refresh()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = new CancellationTokenSource();
            var cancellationToken = _refreshCts.Token;

            try
            {
                switch (ActiveTabIndex)
                {
                    case 0:
                        await RefreshRevenueReport(cancellationToken);
                        break;
                    case 1:
                        await RefreshStockInOutTonReport(cancellationToken);
                        break;
                    case 2:
                        await RefreshStockLedgerReport(cancellationToken);
                        break;
                    case 3:
                        await RefreshSerialTraceReport(cancellationToken);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        // --- TAB 1: DOANH THU & LỢI NHUẬN ---
        private async Task RefreshRevenueReport(CancellationToken cancellationToken)
        {
            try
            {
                using var db = _contextFactory();
                var startDate = FromDate.Date;
                var endDate = ToDate.Date.AddDays(1).AddTicks(-1);

                var sales = await db.SalesInvoices
                    .Where(s => s.InvoiceDate >= startDate && s.InvoiceDate <= endDate)
                    .Select(s => new { s.InvoiceDate, s.GrandTotal })
                    .ToListAsync(cancellationToken);

                var purchases = await db.PurchaseInvoices
                    .Where(p => p.InvoiceDate >= startDate && p.InvoiceDate <= endDate)
                    .Select(p => new { p.InvoiceDate, p.GrandTotal })
                    .ToListAsync(cancellationToken);

                TotalRevenue = sales.Sum(s => s.GrandTotal);
                TotalCost = purchases.Sum(p => p.GrandTotal);
                TotalProfit = TotalRevenue - TotalCost;

                var dailySales = sales
                    .GroupBy(s => s.InvoiceDate.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(s => s.GrandTotal));

                var dailyPurchases = purchases
                    .GroupBy(p => p.InvoiceDate.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.GrandTotal));

                var allDates = dailySales.Keys.Union(dailyPurchases.Keys).OrderBy(d => d).ToList();

                DailyReports.Clear();
                var tempReports = new List<DailyReportItem>();
                foreach (var date in allDates)
                {
                    dailySales.TryGetValue(date, out decimal rev);
                    dailyPurchases.TryGetValue(date, out decimal cost);
                    tempReports.Add(new DailyReportItem
                    {
                        Date = date,
                        Revenue = rev,
                        Cost = cost
                    });
                }
                DailyReports = new ObservableCollection<DailyReportItem>(tempReports);

                // Cập nhật biểu đồ LiveCharts2
                if (tempReports.Any())
                {
                    RevenueExpenseSeries = new ISeries[]
                    {
                        new LineSeries<decimal>
                        {
                            Name = "Doanh thu",
                            Values = tempReports.Select(r => r.Revenue).ToArray(),
                            Stroke = new SolidColorPaint(SKColors.ForestGreen, 3),
                            GeometryStroke = new SolidColorPaint(SKColors.ForestGreen, 3),
                            GeometrySize = 6,
                            Fill = new SolidColorPaint(SKColors.ForestGreen.WithAlpha(30))
                        },
                        new LineSeries<decimal>
                        {
                            Name = "Chi phí",
                            Values = tempReports.Select(r => r.Cost).ToArray(),
                            Stroke = new SolidColorPaint(SKColors.Crimson, 3),
                            GeometryStroke = new SolidColorPaint(SKColors.Crimson, 3),
                            GeometrySize = 6,
                            Fill = new SolidColorPaint(SKColors.Crimson.WithAlpha(30))
                        }
                    };

                    RevenueExpenseXAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = tempReports.Select(r => r.Date.ToString("dd/MM")).ToArray(),
                            LabelsRotation = 15
                        }
                    };
                }
                else
                {
                    RevenueExpenseSeries = Array.Empty<ISeries>();
                    RevenueExpenseXAxes = Array.Empty<Axis>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải báo cáo doanh thu: {ex.Message}");
            }
        }

        // --- TAB 2: XUẤT NHẬP TỒN TỔNG HỢP ---
        private async Task RefreshStockInOutTonReport(CancellationToken cancellationToken)
        {
            try
            {
                using var db = _contextFactory();
                var startDate = FromDate.Date;
                var endDate = ToDate.Date.AddDays(1).AddTicks(-1);

                var productQuery = db.Products
                    .AsNoTracking()
                    .Include(product => product.Category)
                    .AsQueryable();

                if (SelectedCategory != null && SelectedCategory.Id > 0)
                {
                    productQuery = productQuery.Where(product => product.CategoryId == SelectedCategory.Id);
                }

                if (!string.IsNullOrWhiteSpace(SearchProductText))
                {
                    var keyword = SearchProductText.ToLower();
                    productQuery = productQuery.Where(product =>
                        product.DisplayName.ToLower().Contains(keyword)
                        || product.ProductCode.ToLower().Contains(keyword));
                }

                var products = await productQuery.ToListAsync(cancellationToken);
                var productIds = products.Select(product => product.Id).ToList();

                var totals = await db.StockLedgers
                    .AsNoTracking()
                    .Where(ledger => productIds.Contains(ledger.ProductId) && ledger.PostedAt <= endDate)
                    .GroupBy(ledger => ledger.ProductId)
                    .Select(group => new
                    {
                        ProductId = group.Key,
                        StartQuantity = group.Sum(ledger => ledger.PostedAt < startDate
                            ? (ledger.MovementType == "In" ? ledger.Quantity : -ledger.Quantity)
                            : 0),
                        InQuantity = group.Sum(ledger => ledger.PostedAt >= startDate
                            && ledger.MovementType == "In" ? ledger.Quantity : 0),
                        OutQuantity = group.Sum(ledger => ledger.PostedAt >= startDate
                            && ledger.MovementType == "Out" ? ledger.Quantity : 0)
                    })
                    .ToDictionaryAsync(item => item.ProductId, cancellationToken);

                var reports = products.Select(product =>
                {
                    totals.TryGetValue(product.Id, out var total);
                    var startQuantity = total?.StartQuantity ?? 0;
                    var inQuantity = total?.InQuantity ?? 0;
                    var outQuantity = total?.OutQuantity ?? 0;
                    var endQuantity = startQuantity + inQuantity - outQuantity;
                    var unitPrice = product.CostPrice ?? product.DefaultPrice;

                    return new StockInOutTonReportItem
                    {
                        ProductCode = product.ProductCode,
                        ProductName = product.DisplayName,
                        UnitName = "Cái",
                        DauKyQty = startQuantity,
                        DauKyValue = startQuantity * unitPrice,
                        NhapQty = inQuantity,
                        NhapValue = inQuantity * unitPrice,
                        XuatQty = outQuantity,
                        XuatValue = outQuantity * unitPrice,
                        CuoiKyQty = endQuantity,
                        CuoiKyValue = endQuantity * unitPrice
                    };
                });

                StockInOutTonReports = new ObservableCollection<StockInOutTonReportItem>(
                    reports.OrderBy(report => report.ProductName));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải báo cáo XNT: {ex.Message}");
            }
        }

        // --- TAB 3: SỔ KHO / THẺ KHO CHI TIẾT ---
        private async Task RefreshStockLedgerReport(CancellationToken cancellationToken)
        {
            try
            {
                if (SelectedProduct == null)
                {
                    LedgerReports.Clear();
                    LedgerStartQty = 0;
                    LedgerEndQty = 0;
                    return;
                }

                var result = await Task.Run(() =>
                    _traceService.GetProductTimeline(SelectedProduct.Id, FromDate, ToDate),
                    cancellationToken);
                LedgerStartQty = result.StartQuantity;
                LedgerEndQty = result.EndQuantity;
                LedgerReports = new ObservableCollection<StockLedgerReportItem>(
                    result.Items
                        .OrderByDescending(r => r.Date)
                        .Select(r => new StockLedgerReportItem
                        {
                            Date = r.Date,
                            ProductCode = r.ProductCode,
                            ProductName = r.ProductName,
                            DocumentCode = r.DocumentCode,
                            SourceDocumentType = r.SourceDocumentType,
                            Purpose = r.Purpose,
                            PartnerName = r.PartnerName,
                            WarehouseName = r.WarehouseName,
                            UserName = r.UserName,
                            InQty = r.InQty,
                            OutQty = r.OutQty,
                            BalanceQty = r.BalanceQty
                        }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        // --- TAB 4: TRUY VẾT SERIAL ---
        private async Task RefreshSerialTraceReport(CancellationToken cancellationToken)
        {
            try
            {
                var result = await Task.Run(() => _traceService.SearchSerialTrace(new SerialTraceFilter
                {
                    SearchText = SearchSerialText,
                    ProductText = SerialProductText,
                    DocumentText = SerialDocumentText,
                    PartnerText = SerialPartnerText,
                    Status = SelectedSerialStatus,
                    FromDate = FromDate,
                    ToDate = ToDate
                }), cancellationToken);

                SerialTraceReports = new ObservableCollection<SerialTraceReportItem>(
                    result.Select(r => new SerialTraceReportItem
                    {
                        SerialNumber = r.SerialNumber,
                        ProductCode = r.ProductCode,
                        ProductName = r.ProductName,
                        CurrentStatus = r.CurrentStatus,
                        CurrentWarehouseName = r.CurrentWarehouseName,
                        ImportDocCode = r.ImportDocCode,
                        ImportDate = r.ImportDate,
                        ImportWarehouseName = r.ImportWarehouseName,
                        SupplierName = r.SupplierName,
                        ExportDocCode = r.ExportDocCode,
                        ExportDate = r.ExportDate,
                        ExportWarehouseName = r.ExportWarehouseName,
                        CustomerName = r.CustomerName,
                        SellPrice = r.SellPrice,
                        SalesInvoiceCode = r.SalesInvoiceCode,
                        SalesInvoiceDate = r.SalesInvoiceDate,
                        WarrantyStatus = r.WarrantyStatus,
                        WarrantyStartDate = r.WarrantyStartDate,
                        WarrantyEndDate = r.WarrantyEndDate,
                        WarrantyCustomerName = r.WarrantyCustomerName
                    }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        [RelayCommand]
        private static void ViewSerialTraceDetail(SerialTraceReportItem? item)
        {
            if (item == null) return;
            var window = new QuanLyHangHoa.Views.SerialTraceDetailWindow(item)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.ShowDialog();
        }
    }

    // --- CÁC LỚP ĐỐI TƯỢNG BÁO CÁO (DTOs) ---

    public class DailyReportItem
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Profit => Revenue - Cost;
    }

    public class StockInOutTonReportItem
    {
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;

        // Tồn đầu kỳ
        public decimal DauKyQty { get; set; }
        public decimal DauKyValue { get; set; }

        // Nhập trong kỳ
        public decimal NhapQty { get; set; }
        public decimal NhapValue { get; set; }

        // Xuất trong kỳ
        public decimal XuatQty { get; set; }
        public decimal XuatValue { get; set; }

        // Tồn cuối kỳ
        public decimal CuoiKyQty { get; set; }
        public decimal CuoiKyValue { get; set; }
    }

    public class StockLedgerReportItem
    {
        public DateTime Date { get; set; }
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string DocumentCode { get; set; } = string.Empty;
        public string SourceDocumentType { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string PartnerName { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public decimal InQty { get; set; }
        public decimal OutQty { get; set; }
        public decimal BalanceQty { get; set; }
    }

    public class SerialTraceReportItem
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public string CurrentWarehouseName { get; set; } = string.Empty;

        // Nhập
        public string ImportDocCode { get; set; } = string.Empty;
        public DateTime? ImportDate { get; set; }
        public string ImportWarehouseName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;

        // Xuất
        public string ExportDocCode { get; set; } = string.Empty;
        public DateTime? ExportDate { get; set; }
        public string ExportWarehouseName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal? SellPrice { get; set; }

        // Bảo hành
        public string SalesInvoiceCode { get; set; } = string.Empty;
        public DateTime? SalesInvoiceDate { get; set; }
        public string WarrantyStatus { get; set; } = string.Empty;
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public string WarrantyCustomerName { get; set; } = string.Empty;
        public string DisplayCustomerName => string.IsNullOrWhiteSpace(WarrantyCustomerName) ? CustomerName : WarrantyCustomerName;
    }
}
