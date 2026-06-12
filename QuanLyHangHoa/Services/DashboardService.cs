using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
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

        public async Task<DashboardStats> GetStatsAsync()
        {
            using var context = _contextFactory();
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfYear = new DateTime(now.Year, 1, 1);

            var stats = new DashboardStats();

            // Inventory
            stats.TotalInventoryCount = (int)await context.StockBalances.SumAsync(sb => sb.OnHandQuantity);

            // Stock In
            stats.StockInMonthCount = await context.StockIns.CountAsync(s => s.CreatedAt >= startOfMonth);

            // Stock Out
            stats.StockOutMonthCount = await context.StockOuts.CountAsync(s => s.CreatedAt >= startOfMonth);

            // Sales & Revenue
            var salesYear = await context.SalesInvoices
                .Where(s => s.InvoiceDate >= startOfYear)
                .Select(s => new { s.InvoiceDate, s.GrandTotal })
                .ToListAsync();

            stats.SalesInvoiceYearCount = salesYear.Count;
            stats.RevenueYear = salesYear.Sum(s => s.GrandTotal);

            var salesMonth = salesYear.Where(s => s.InvoiceDate >= startOfMonth).ToList();
            stats.SalesInvoiceMonthCount = salesMonth.Count;
            stats.RevenueMonth = salesMonth.Sum(s => s.GrandTotal);

            // Unpaid Invoices
            stats.UnpaidSalesInvoiceCount = await context.SalesInvoices
                .CountAsync(s => s.PaymentStatus == "Unpaid" || s.PaymentStatus == "Partial" || s.PaymentStatus == "Overdue");
            stats.UnpaidPurchaseInvoiceCount = await context.PurchaseInvoices
                .CountAsync(p => p.PaymentStatus == "Unpaid" || p.PaymentStatus == "Partial" || p.PaymentStatus == "Overdue");

            // Warranty
            stats.WarrantyActiveCount = await context.WarrantyClaims
                .CountAsync(w => w.Status == "Active" || w.Status == "Processing");

            // Recent Activity
            var rawStockIns = await context.StockIns
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => new { Type = "In", s.DocumentCode, s.CreatedAt })
                .ToListAsync();

            var rawStockOuts = await context.StockOuts
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => new { Type = "Out", s.DocumentCode, s.CreatedAt })
                .ToListAsync();

            var combinedActivities = rawStockIns
                .Concat(rawStockOuts)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .Select(a => new RecentActivity
                {
                    Title = (a.Type == "In" ? "Nhập kho: " : "Xuất kho: ") + a.DocumentCode,
                    TimeAgo = GetRelativeTime(a.CreatedAt),
                    IconKind = a.Type == "In" ? "ArrowDown" : "ArrowUp",
                    IconColor = a.Type == "In" ? "#10B981" : "#3B82F6"
                })
                .ToList();

            stats.Activities = combinedActivities;

            // Load chart data concurrently
            var revenueExpenseTask = GetRevenueAndExpenseChartDataAsync(6); // 6 months
            var inventoryStructureTask = GetInventoryStructureChartDataAsync();
            var topSellingTask = GetTopSellingProductsAsync(5); // Top 5
            var stockMovementTask = GetStockMovementTrendAsync(7); // 7 days

            await Task.WhenAll(revenueExpenseTask, inventoryStructureTask, topSellingTask, stockMovementTask);

            stats.RevenueExpenseChart = await revenueExpenseTask;
            stats.InventoryStructureChart = await inventoryStructureTask;
            stats.TopSellingProductsChart = await topSellingTask;
            stats.StockMovementChart = await stockMovementTask;

            return stats;
        }

        public async Task<System.Collections.Generic.List<RevenueExpenseData>> GetRevenueAndExpenseChartDataAsync(int months)
        {
            using var context = _contextFactory();
            var now = DateTime.Now;
            var startDate = new DateTime(now.Year, now.Month, 1).AddMonths(-months + 1);

            var sales = await context.SalesInvoices
                .Where(s => s.InvoiceDate >= startDate)
                .Select(s => new { s.InvoiceDate.Year, s.InvoiceDate.Month, s.GrandTotal })
                .ToListAsync();

            var purchases = await context.PurchaseInvoices
                .Where(p => p.InvoiceDate >= startDate)
                .Select(p => new { p.InvoiceDate.Year, p.InvoiceDate.Month, p.GrandTotal })
                .ToListAsync();

            var result = new System.Collections.Generic.List<RevenueExpenseData>();
            for (int i = 0; i < months; i++)
            {
                var date = startDate.AddMonths(i);
                var monthStr = date.ToString("MM/yyyy");

                var monthlySales = sales.Where(s => s.Year == date.Year && s.Month == date.Month).Sum(s => s.GrandTotal);
                var monthlyPurchases = purchases.Where(p => p.Year == date.Year && p.Month == date.Month).Sum(p => p.GrandTotal);

                result.Add(new RevenueExpenseData
                {
                    Month = monthStr,
                    Revenue = monthlySales,
                    Expense = monthlyPurchases
                });
            }

            return result;
        }

        public async Task<System.Collections.Generic.List<InventoryStructureData>> GetInventoryStructureChartDataAsync()
        {
            using var context = _contextFactory();
            var balances = await context.StockBalances
                .Include(sb => sb.Product)
                    .ThenInclude(p => p.Category)
                .ToListAsync();

            var grouped = balances
                .GroupBy(sb => sb.Product.CategoryName)
                .Select(g => new InventoryStructureData
                {
                    CategoryName = g.Key,
                    TotalValue = g.Sum(sb => sb.OnHandQuantity * (sb.Product.CostPrice ?? sb.Product.DefaultPrice))
                })
                .OrderByDescending(x => x.TotalValue)
                .ToList();

            return grouped;
        }

        public async Task<System.Collections.Generic.List<TopSellingProductData>> GetTopSellingProductsAsync(int limit)
        {
            using var context = _contextFactory();
            var grouped = await context.SalesInvoiceLines
                .Include(l => l.Product)
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

        public async Task<System.Collections.Generic.List<StockMovementData>> GetStockMovementTrendAsync(int days)
        {
            using var context = _contextFactory();
            var startDate = DateTime.Today.AddDays(-days + 1);

            var stockIns = await context.StockIns
                .Where(s => s.CreatedAt >= startDate)
                .Select(s => s.CreatedAt.Date)
                .ToListAsync();

            var stockOuts = await context.StockOuts
                .Where(s => s.CreatedAt >= startDate)
                .Select(s => s.CreatedAt.Date)
                .ToListAsync();

            var result = new System.Collections.Generic.List<StockMovementData>();
            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                var dateStr = date.ToString("dd/MM");

                result.Add(new StockMovementData
                {
                    Date = dateStr,
                    StockInCount = stockIns.Count(d => d == date),
                    StockOutCount = stockOuts.Count(d => d == date)
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
