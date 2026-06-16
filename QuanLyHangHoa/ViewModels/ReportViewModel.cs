using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace QuanLyHangHoa.ViewModels
{
    public partial class ReportViewModel : ObservableObject
    {
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
        [ObservableProperty] private ObservableCollection<SerialTraceReportItem> _serialTraceReports = new();

        public ReportViewModel()
        {
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
                using var db = new AppDbContext();
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
                using var db = new AppDbContext();
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
                using var db = new AppDbContext();
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

                using var db = new AppDbContext();
                var startDate = FromDate.Date;
                var endDate = ToDate.Date.AddDays(1).AddTicks(-1);

                // Lấy toàn bộ Ledger của sản phẩm
                var allLedgers = db.StockLedgers
                    .Where(l => l.ProductId == SelectedProduct.Id && l.PostedAt <= endDate)
                    .OrderBy(l => l.PostedAt)
                    .ToList();

                // Tính tồn đầu kỳ
                decimal currentQty = allLedgers
                    .Where(l => l.PostedAt < startDate)
                    .Sum(l => l.MovementType == "In" ? l.Quantity : -l.Quantity);
                LedgerStartQty = currentQty;

                var currentLedgers = allLedgers.Where(l => l.PostedAt >= startDate && l.PostedAt <= endDate).ToList();
                var reportList = new List<StockLedgerReportItem>();

                // Để lấy tên đối tác và mã chứng từ nhanh, ta tải trước thông tin chứng từ liên quan
                var stockInIds = currentLedgers.Where(l => l.SourceDocumentType == "StockIn").Select(l => l.SourceDocumentId).Distinct().ToList();
                var stockIns = db.StockIns.Include(s => s.Supplier).Where(s => stockInIds.Contains(s.Id)).ToDictionary(s => s.Id);

                var stockOutIds = currentLedgers.Where(l => l.SourceDocumentType == "StockOut").Select(l => l.SourceDocumentId).Distinct().ToList();
                var stockOuts = db.StockOuts.Include(s => s.Customer).Where(s => stockOutIds.Contains(s.Id)).ToDictionary(s => s.Id);

                var adjustmentIds = currentLedgers.Where(l => l.SourceDocumentType == "StockAdjustment").Select(l => l.SourceDocumentId).Distinct().ToList();
                var adjustments = db.StockAdjustments.Where(s => adjustmentIds.Contains(s.Id)).ToDictionary(s => s.Id);

                foreach (var l in currentLedgers)
                {
                    string docCode = $"Ref-{l.SourceDocumentId}";
                    string purpose = l.SourceDocumentType;
                    string partner = "-";

                    if (l.SourceDocumentType == "StockIn" && stockIns.TryGetValue(l.SourceDocumentId, out var si))
                    {
                        docCode = si.DocumentCode;
                        purpose = si.PurposeCode == "Purchase" ? "Nhập mua" : (si.PurposeCode == "OpeningBalance" ? "Nhập tồn đầu" : "Nhập điều chỉnh");
                        partner = si.Supplier?.DisplayName ?? "-";
                    }
                    else if (l.SourceDocumentType == "StockOut" && stockOuts.TryGetValue(l.SourceDocumentId, out var so))
                    {
                        docCode = so.DocumentCode;
                        purpose = so.PurposeCode == "Sale" ? "Xuất bán" : (so.PurposeCode == "WarrantyReplacement" ? "Xuất bảo hành" : "Xuất điều chỉnh");
                        partner = so.Customer?.DisplayName ?? "-";
                    }
                    else if (l.SourceDocumentType == "StockAdjustment" && adjustments.TryGetValue(l.SourceDocumentId, out var sa))
                    {
                        docCode = sa.DocumentCode;
                        purpose = "Kiểm kê / Điều chỉnh";
                        partner = "Hệ thống";
                    }

                    decimal inQty = l.MovementType == "In" ? l.Quantity : 0;
                    decimal outQty = l.MovementType == "Out" ? l.Quantity : 0;
                    currentQty += (inQty - outQty);

                    reportList.Add(new StockLedgerReportItem
                    {
                        Date = l.PostedAt,
                        DocumentCode = docCode,
                        Purpose = purpose,
                        PartnerName = partner,
                        InQty = inQty,
                        OutQty = outQty,
                        BalanceQty = currentQty
                    });
                }

                LedgerEndQty = currentQty;
                LedgerReports = new ObservableCollection<StockLedgerReportItem>(reportList.OrderByDescending(r => r.Date));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải sổ kho chi tiết: {ex.Message}");
            }
        }

        // --- TAB 4: TRUY VẾT SERIAL ---
        private void RefreshSerialTraceReport()
        {
            try
            {
                using var db = new AppDbContext();
                
                var query = db.ProductSerials
                    .Include(s => s.Product)
                    .Include(s => s.LastStockInLine)
                        .ThenInclude(l => l.StockIn)
                            .ThenInclude(si => si.Supplier)
                    .Include(s => s.LastStockOutLine)
                        .ThenInclude(l => l.StockOut)
                            .ThenInclude(so => so.Customer)
                    .Include(s => s.WarrantyCoverage)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(SearchSerialText))
                {
                    var serialKeyword = SearchSerialText.Trim().ToLower();
                    query = query.Where(s => s.SerialNumber.ToLower().Contains(serialKeyword));
                }

                // Giới hạn kết xuất tối đa 100 dòng để bảo đảm hiệu năng
                var serials = query.OrderBy(s => s.SerialNumber).Take(100).ToList();
                var reportList = new List<SerialTraceReportItem>();

                foreach (var s in serials)
                {
                    string wStatus = "Chưa bán (Trong kho)";
                    if (s.LastStockOutLine != null)
                    {
                        if (s.WarrantyCoverage != null)
                        {
                            wStatus = (s.WarrantyCoverage.CoverageStatus == "Active" && s.WarrantyCoverage.WarrantyEndDate >= DateTime.Now)
                                ? "Còn bảo hành"
                                : "Hết hạn bảo hành";
                        }
                        else
                        {
                            wStatus = "Không có bảo hành";
                        }
                    }

                    reportList.Add(new SerialTraceReportItem
                    {
                        SerialNumber = s.SerialNumber,
                        ProductName = s.Product.DisplayName,
                        
                        ImportDocCode = s.LastStockInLine.StockIn.DocumentCode,
                        ImportDate = s.LastStockInLine.StockIn.CreatedAt,
                        SupplierName = s.LastStockInLine.StockIn.Supplier?.DisplayName ?? "-",
                        
                        ExportDocCode = s.LastStockOutLine?.StockOut.DocumentCode ?? "-",
                        ExportDate = s.LastStockOutLine?.StockOut.CreatedAt,
                        CustomerName = s.LastStockOutLine?.StockOut.Customer?.DisplayName ?? "-",
                        SellPrice = s.LastStockOutLine?.UnitPrice,
                        
                        WarrantyStatus = wStatus,
                        WarrantyEndDate = s.WarrantyCoverage?.WarrantyEndDate
                    });
                }

                SerialTraceReports = new ObservableCollection<SerialTraceReportItem>(reportList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tải báo cáo truy vết Serial: {ex.Message}");
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
        public string DocumentCode { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string PartnerName { get; set; } = string.Empty;
        public decimal InQty { get; set; }
        public decimal OutQty { get; set; }
        public decimal BalanceQty { get; set; }
    }

    public class SerialTraceReportItem
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;

        // Nhập
        public string ImportDocCode { get; set; } = string.Empty;
        public DateTime? ImportDate { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        // Xuất
        public string ExportDocCode { get; set; } = string.Empty;
        public DateTime? ExportDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal? SellPrice { get; set; }

        // Bảo hành
        public string WarrantyStatus { get; set; } = string.Empty;
        public DateTime? WarrantyEndDate { get; set; }
    }
}
