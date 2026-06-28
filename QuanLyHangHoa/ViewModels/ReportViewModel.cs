using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
            LoadFilterData();
            Refresh();
        }

        // Tự động tải lại dữ liệu khi người dùng chuyển Tab
        partial void OnActiveTabIndexChanged(int value)
        {
            Refresh();
        }

        partial void OnSelectedCategoryChanged(Category? value) => Refresh();
        partial void OnSearchProductTextChanged(string value) => Refresh();

        // Tải danh sách bộ lọc ban đầu (Sản phẩm & Danh mục)
        private void LoadFilterData()
        {
            try
            {
                using var db = _contextFactory();
                var activeCategories = db.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayName).ToList();
                Categories = new ObservableCollection<Category>(activeCategories);
                Categories.Insert(0, new Category { Id = 0, DisplayName = "Tất cả danh mục" });
                SelectedCategory = Categories.FirstOrDefault();

                var activeProducts = db.Products.Where(p => p.IsActive).OrderBy(p => p.DisplayName).ToList();
                Products = new ObservableCollection<Product>(activeProducts);
                if (activeProducts.Any())
                {
                    SelectedProduct = activeProducts.First();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi nạp danh sách bộ lọc: {ex.Message}");
            }
        }

        [RelayCommand]
        public void Refresh()
        {
            switch (ActiveTabIndex)
            {
                case 0:
                    RefreshRevenueReport();
                    break;
                case 1:
                    RefreshStockInOutTonReport();
                    break;
                case 2:
                    RefreshStockLedgerReport();
                    break;
                case 3:
                    RefreshSerialTraceReport();
                    break;
            }
        }

        // --- TAB 1: DOANH THU & LỢI NHUẬN ---
        private void RefreshRevenueReport()
        {
            try
            {
                using var db = _contextFactory();
                var startDate = FromDate.Date;
                var endDate = ToDate.Date.AddDays(1).AddTicks(-1);

                var sales = db.SalesInvoices
                    .Where(s => s.InvoiceDate >= startDate && s.InvoiceDate <= endDate)
                    .Select(s => new { s.InvoiceDate, s.GrandTotal })
                    .ToList();

                var purchases = db.PurchaseInvoices
                    .Where(p => p.InvoiceDate >= startDate && p.InvoiceDate <= endDate)
                    .Select(p => new { p.InvoiceDate, p.GrandTotal })
                    .ToList();

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
        private void RefreshStockInOutTonReport()
        {
            try
            {
                using var db = _contextFactory();
                var startDate = FromDate.Date;
                var endDate = ToDate.Date.AddDays(1).AddTicks(-1);

                // Lấy danh sách sản phẩm theo bộ lọc danh mục và từ khóa (bao gồm cả sản phẩm Inactive có số dư/phát sinh)
                var prodQuery = db.Products.Include(p => p.Category).AsQueryable();
                if (SelectedCategory != null && SelectedCategory.Id > 0)
                {
                    prodQuery = prodQuery.Where(p => p.CategoryId == SelectedCategory.Id);
                }
                if (!string.IsNullOrWhiteSpace(SearchProductText))
                {
                    var kw = SearchProductText.ToLower();
                    prodQuery = prodQuery.Where(p => p.DisplayName.ToLower().Contains(kw) || p.ProductCode.ToLower().Contains(kw));
                }
                var targetProducts = prodQuery.ToList();

                // Lấy toàn bộ giao dịch kho liên quan đến các sản phẩm này
                var targetProductIds = targetProducts.Select(p => p.Id).ToList();
                var ledgers = db.StockLedgers
                    .Where(l => targetProductIds.Contains(l.ProductId) && l.PostedAt <= endDate)
                    .ToList();

                var reportList = new List<StockInOutTonReportItem>();

                foreach (var p in targetProducts)
                {
                    var pLedgers = ledgers.Where(l => l.ProductId == p.Id).ToList();
                    
                    // Đơn giá tính giá trị kho (Giá vốn, nếu null lấy Giá bán lẻ)
                    decimal unitPrice = p.CostPrice ?? p.DefaultPrice;

                    // Tồn đầu kỳ (Giao dịch trước ngày startDate)
                    var dauKyLedgers = pLedgers.Where(l => l.PostedAt < startDate).ToList();
                    decimal dauKyQty = dauKyLedgers.Sum(l => l.MovementType == "In" ? l.Quantity : -l.Quantity);
                    decimal dauKyVal = dauKyQty * unitPrice;

                    // Nhập trong kỳ
                    var trongKyNhapLedgers = pLedgers.Where(l => l.PostedAt >= startDate && l.PostedAt <= endDate && l.MovementType == "In").ToList();
                    decimal nhapQty = trongKyNhapLedgers.Sum(l => l.Quantity);
                    decimal nhapVal = nhapQty * unitPrice;

                    // Xuất trong kỳ
                    var trongKyXuatLedgers = pLedgers.Where(l => l.PostedAt >= startDate && l.PostedAt <= endDate && l.MovementType == "Out").ToList();
                    decimal xuatQty = trongKyXuatLedgers.Sum(l => l.Quantity);
                    decimal xuatVal = xuatQty * unitPrice;

                    // Tồn cuối kỳ
                    decimal cuoiKyQty = dauKyQty + nhapQty - xuatQty;
                    decimal cuoiKyVal = cuoiKyQty * unitPrice;

                    reportList.Add(new StockInOutTonReportItem
                    {
                        ProductCode = p.ProductCode,
                        ProductName = p.DisplayName,
                        UnitName = "Cái", // Đơn vị tính mặc định
                        DauKyQty = dauKyQty,
                        DauKyValue = dauKyVal,
                        NhapQty = nhapQty,
                        NhapValue = nhapVal,
                        XuatQty = xuatQty,
                        XuatValue = xuatVal,
                        CuoiKyQty = cuoiKyQty,
                        CuoiKyValue = cuoiKyVal
                    });
                }

                StockInOutTonReports = new ObservableCollection<StockInOutTonReportItem>(reportList.OrderBy(r => r.ProductName));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải báo cáo XNT: {ex.Message}");
            }
        }

        // --- TAB 3: SỔ KHO / THẺ KHO CHI TIẾT ---
        private void RefreshStockLedgerReport()
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

                var result = _traceService.GetProductTimeline(SelectedProduct.Id, FromDate, ToDate);
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
        private void RefreshSerialTraceReport()
        {
            try
            {
                var result = _traceService.SearchSerialTrace(new SerialTraceFilter
                {
                    SearchText = SearchSerialText,
                    ProductText = SerialProductText,
                    DocumentText = SerialDocumentText,
                    PartnerText = SerialPartnerText,
                    Status = SelectedSerialStatus,
                    FromDate = FromDate,
                    ToDate = ToDate
                });

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
    }
}
