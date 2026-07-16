using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Services
{
    public class RecentActivity
    {
        public string? Title { get; set; }
        public string? TimeAgo { get; set; }
        public string? IconKind { get; set; }
        public string? IconColor { get; set; }
    }

    public class RevenueExpenseData
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Expense { get; set; }
    }

    public class InventoryStructureData
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalValue { get; set; }
    }

    public class TopSellingProductData
    {
        public string ProductName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
    }

    public class StockMovementData
    {
        public string Date { get; set; } = string.Empty;
        public int StockInCount { get; set; }
        public int StockOutCount { get; set; }
    }

    public class DashboardStats
    {
        public int TotalInventoryCount { get; set; }
        public int StockInMonthCount { get; set; }
        public int StockOutMonthCount { get; set; }
        public int WarrantyActiveCount { get; set; }
        public int UnpaidPurchaseInvoiceCount { get; set; }
        public int UnpaidSalesInvoiceCount { get; set; }
        public int SalesInvoiceMonthCount { get; set; }
        public int SalesInvoiceYearCount { get; set; }
        public decimal RevenueMonth { get; set; }
        public decimal RevenueYear { get; set; }
        public System.Collections.Generic.List<RecentActivity> Activities { get; set; } = new();
        public System.Collections.Generic.List<RevenueExpenseData> RevenueExpenseChart { get; set; } = new();
        public System.Collections.Generic.List<InventoryStructureData> InventoryStructureChart { get; set; } = new();
        public System.Collections.Generic.List<TopSellingProductData> TopSellingProductsChart { get; set; } = new();
        public System.Collections.Generic.List<StockMovementData> StockMovementChart { get; set; } = new();
    }

    public class DashboardService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public DashboardService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // bốn biểu đồ bắt đầu trước để chạy song song với các chỉ số chính; mỗi task tự sở hữu một DbContext
        public virtual async Task<DashboardStats> GetStatsAsync()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfYear = new DateTime(now.Year, 1, 1);

            var revenueExpenseTask = GetRevenueAndExpenseChartDataAsync(6);
            var inventoryStructureTask = GetInventoryStructureChartDataAsync();
            var topSellingTask = GetTopSellingProductsAsync(5);
            var stockMovementTask = GetStockMovementTrendAsync(7);

            using var context = _contextFactory();
            var stats = new DashboardStats();

            // tổng tồn là số lượng đơn vị cơ sở đang có trong mọi kho
            stats.TotalInventoryCount = (int)(await context.StockBalances
                .AsNoTracking()
                .SumAsync(sb => (decimal?)sb.OnHandQuantity) ?? 0);

            // đếm số chứng từ nhập được tạo từ đầu tháng hiện tại
            stats.StockInMonthCount = await context.StockIns
                .AsNoTracking()
                .CountAsync(s => s.CreatedAt >= startOfMonth);

            // đếm số chứng từ xuất được tạo từ đầu tháng hiện tại
            stats.StockOutMonthCount = await context.StockOuts
                .AsNoTracking()
                .CountAsync(s => s.CreatedAt >= startOfMonth);

            // gom một lần ở database để lấy cả doanh thu/số hóa đơn tháng và năm
            var salesSummary = await context.SalesInvoices
                .AsNoTracking()
                .Where(s => s.InvoiceDate >= startOfYear)
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    YearCount = group.Count(),
                    YearRevenue = group.Sum(invoice => invoice.GrandTotal),
                    MonthCount = group.Count(invoice => invoice.InvoiceDate >= startOfMonth),
                    MonthRevenue = group
                        .Where(invoice => invoice.InvoiceDate >= startOfMonth)
                        .Sum(invoice => (decimal?)invoice.GrandTotal) ?? 0
                })
                .SingleOrDefaultAsync();

            if (salesSummary != null)
            {
                stats.SalesInvoiceYearCount = salesSummary.YearCount;
                stats.RevenueYear = salesSummary.YearRevenue;
                stats.SalesInvoiceMonthCount = salesSummary.MonthCount;
                stats.RevenueMonth = salesSummary.MonthRevenue;
            }

            // mọi trạng thái khác Paid đều được xem là công nợ trên dashboard
            stats.UnpaidSalesInvoiceCount = await context.SalesInvoices
                .AsNoTracking()
                .CountAsync(s => s.PaymentStatus != PaymentStatus.Paid);
            stats.UnpaidPurchaseInvoiceCount = await context.PurchaseInvoices
                .AsNoTracking()
                .CountAsync(p => p.PaymentStatus != PaymentStatus.Paid);

            // chỉ đếm claim đang hoạt động hoặc đang xử lý
            stats.WarrantyActiveCount = await context.WarrantyClaims
                .AsNoTracking()
                .CountAsync(w => w.Status == "Active" || w.Status == "Processing");

            // gộp nhập và xuất trước khi sắp xếp để lấy đúng năm hoạt động mới nhất toàn hệ thống
            var combinedActivities = await context.StockIns
                .AsNoTracking()
                .Select(s => new { Type = "In", s.DocumentCode, s.CreatedAt })
                .Concat(context.StockOuts
                    .AsNoTracking()
                    .Select(s => new { Type = "Out", s.DocumentCode, s.CreatedAt }))
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync();

            var activities = combinedActivities
                .Select(a => new RecentActivity
                {
                    Title = (a.Type == "In" ? "Nhập kho: " : "Xuất kho: ") + a.DocumentCode,
                    TimeAgo = GetRelativeTime(a.CreatedAt),
                    IconKind = a.Type == "In" ? "ArrowDown" : "ArrowUp",
                    IconColor = a.Type == "In" ? "#10B981" : "#3B82F6"
                })
                .ToList();

            stats.Activities = activities;

            await Task.WhenAll(revenueExpenseTask, inventoryStructureTask, topSellingTask, stockMovementTask);

            stats.RevenueExpenseChart = await revenueExpenseTask;
            stats.InventoryStructureChart = await inventoryStructureTask;
            stats.TopSellingProductsChart = await topSellingTask;
            stats.StockMovementChart = await stockMovementTask;

            return stats;
        }

        // dictionary theo (năm, tháng) giúp lấp cả tháng không phát sinh bằng giá trị 0
        public async Task<System.Collections.Generic.List<RevenueExpenseData>> GetRevenueAndExpenseChartDataAsync(int months)
        {
            using var context = _contextFactory();
            var now = DateTime.Now;
            var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-months + 1);

            var sales = await context.SalesInvoices
                .AsNoTracking()
                .Where(s => s.InvoiceDate >= startDate)
                .GroupBy(s => new { s.InvoiceDate.Year, s.InvoiceDate.Month })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Total = group.Sum(invoice => invoice.GrandTotal)
                })
                .ToListAsync();

            var purchases = await context.PurchaseInvoices
                .AsNoTracking()
                .Where(p => p.InvoiceDate >= startDate)
                .GroupBy(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    Total = group.Sum(invoice => invoice.GrandTotal)
                })
                .ToListAsync();

            var salesByMonth = sales.ToDictionary(
                item => (item.Year, item.Month),
                item => item.Total);
            var purchasesByMonth = purchases.ToDictionary(
                item => (item.Year, item.Month),
                item => item.Total);

            var result = new System.Collections.Generic.List<RevenueExpenseData>(months);
            for (int i = 0; i < months; i++)
            {
                var date = startDate.AddMonths(i);
                salesByMonth.TryGetValue((date.Year, date.Month), out var monthlySales);
                purchasesByMonth.TryGetValue((date.Year, date.Month), out var monthlyPurchases);

                result.Add(new RevenueExpenseData
                {
                    Month = date.ToString("MM/yyyy"),
                    Revenue = monthlySales,
                    Expense = monthlyPurchases
                });
            }

            return result;
        }

        // giá trị tồn ưu tiên giá vốn, thiếu giá vốn mới dùng giá bán mặc định
        public async Task<System.Collections.Generic.List<InventoryStructureData>> GetInventoryStructureChartDataAsync()
        {
            using var context = _contextFactory();
            return await context.StockBalances
                .AsNoTracking()
                .GroupBy(balance => balance.Product.Category.DisplayName)
                .Select(group => new InventoryStructureData
                {
                    CategoryName = group.Key,
                    TotalValue = group.Sum(balance =>
                        balance.OnHandQuantity * (balance.Product.CostPrice ?? balance.Product.DefaultPrice))
                })
                .OrderByDescending(item => item.TotalValue)
                .ToListAsync();
        }

        // tổng quantity được nhóm tại database rồi mới lấy giới hạn sản phẩm bán nhiều nhất
        public async Task<System.Collections.Generic.List<TopSellingProductData>> GetTopSellingProductsAsync(int limit)
        {
            using var context = _contextFactory();
            var grouped = await context.SalesInvoiceLines
                .AsNoTracking()
                .GroupBy(l => l.Product.DisplayName)
                .Select(g => new TopSellingProductData
                {
                    ProductName = g.Key,
                    TotalSold = (int)g.Sum(l => l.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(limit)
                .ToListAsync();

            return grouped;
        }

        // tạo đủ từng ngày trong khoảng để biểu đồ không bị đứt khi ngày đó không có chứng từ
        public async Task<System.Collections.Generic.List<StockMovementData>> GetStockMovementTrendAsync(int days)
        {
            using var context = _contextFactory();
            var startDate = DateTime.Today.AddDays(-days + 1);

            var stockIns = await context.StockIns
                .AsNoTracking()
                .Where(s => s.CreatedAt >= startDate)
                .GroupBy(s => s.CreatedAt.Date)
                .Select(group => new { Date = group.Key, Count = group.Count() })
                .ToListAsync();

            var stockOuts = await context.StockOuts
                .AsNoTracking()
                .Where(s => s.CreatedAt >= startDate)
                .GroupBy(s => s.CreatedAt.Date)
                .Select(group => new { Date = group.Key, Count = group.Count() })
                .ToListAsync();

            var stockInsByDate = stockIns.ToDictionary(item => item.Date, item => item.Count);
            var stockOutsByDate = stockOuts.ToDictionary(item => item.Date, item => item.Count);

            var result = new System.Collections.Generic.List<StockMovementData>(days);
            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                stockInsByDate.TryGetValue(date, out var stockInCount);
                stockOutsByDate.TryGetValue(date, out var stockOutCount);

                result.Add(new StockMovementData
                {
                    Date = date.ToString("dd/MM"),
                    StockInCount = stockInCount,
                    StockOutCount = stockOutCount
                });
            }

            return result;
        }

        private string GetRelativeTime(DateTime date)
        {
            var ts = DateTime.Now - date;
            if (ts.TotalMinutes < 1) return "Vừa xong";
            if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes} phút trước";
            if (ts.TotalHours < 24) return $"{(int)ts.TotalHours} giờ trước";
            return date.ToString("dd/MM/yyyy");
        }
    }
}
