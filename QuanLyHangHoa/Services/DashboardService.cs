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
            // Unpaid Invoices (Unpaid / Partial - stored as English in DB)
            stats.UnpaidSalesInvoiceCount = await context.SalesInvoices
                .CountAsync(s => s.PaymentStatus == "Unpaid" || s.PaymentStatus == "Partial" || s.PaymentStatus == "Overdue");
            stats.UnpaidPurchaseInvoiceCount = await context.PurchaseInvoices
                .CountAsync(p => p.PaymentStatus == "Unpaid" || p.PaymentStatus == "Partial" || p.PaymentStatus == "Overdue");

            // Warranty
            stats.WarrantyActiveCount = await context.WarrantyClaims
                .CountAsync(w => w.Status == "Active" || w.Status == "Processing");

            // Recent Activity
            var recentStockIns = await context.StockIns
                .OrderByDescending(s => s.CreatedAt)
                .Take(3)
                .Select(s => new RecentActivity 
                { 
                    Title = "Nhập kho: " + s.DocumentCode, 
                    TimeAgo = GetRelativeTime(s.CreatedAt),
                    IconKind = "ArrowDown",
                    IconColor = "#10B981"
                })
                .ToListAsync();

            var recentStockOuts = await context.StockOuts
                .OrderByDescending(s => s.CreatedAt)
                .Take(3)
                .Select(s => new RecentActivity 
                { 
                    Title = "Xuất kho: " + s.DocumentCode, 
                    TimeAgo = GetRelativeTime(s.CreatedAt),
                    IconKind = "ArrowUp",
                    IconColor = "#3B82F6"
                })
                .ToListAsync();

            stats.Activities.AddRange(recentStockIns);
            stats.Activities.AddRange(recentStockOuts);
            stats.Activities = stats.Activities.OrderByDescending(a => a.TimeAgo).Take(5).ToList();

            return stats;
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
