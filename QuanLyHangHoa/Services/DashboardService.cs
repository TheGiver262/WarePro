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
        public int StockInYearCount { get; set; }
        public int SalesInvoiceMonthCount { get; set; }
        public int SalesInvoiceYearCount { get; set; }
        public decimal RevenueMonth { get; set; }
        public decimal RevenueYear { get; set; }
        public int WarrantyActiveCount { get; set; }
        public System.Collections.Generic.List<RecentActivity> Activities { get; set; } = new();
    }

    public class DashboardService
    {
        private readonly AppDbContext _context;

        public DashboardService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DashboardStats> GetStatsAsync()
        {
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var startOfYear = new DateTime(now.Year, 1, 1);

            var stats = new DashboardStats();

            // Inventory - Use Sum in DB if possible, but SQLite decimal Sum is tricky. 
            // Better to sum OnHandQuantity directly.
            stats.TotalInventoryCount = (int)await _context.StockBalances.SumAsync(sb => sb.OnHandQuantity);

            // Stock In
            stats.StockInMonthCount = await _context.StockIns.CountAsync(s => s.CreatedAt >= startOfMonth);
            stats.StockInYearCount = await _context.StockIns.CountAsync(s => s.CreatedAt >= startOfYear);

            // Sales & Revenue - Fetch year data and split in memory for performance
            var salesYear = await _context.SalesInvoices
                .Where(s => s.InvoiceDate >= startOfYear)
                .Select(s => new { s.InvoiceDate, s.GrandTotal })
                .ToListAsync();

            stats.SalesInvoiceYearCount = salesYear.Count;
            stats.RevenueYear = salesYear.Sum(s => s.GrandTotal);

            var salesMonth = salesYear.Where(s => s.InvoiceDate >= startOfMonth).ToList();
            stats.SalesInvoiceMonthCount = salesMonth.Count;
            stats.RevenueMonth = salesMonth.Sum(s => s.GrandTotal);

            // Warranty
            stats.WarrantyActiveCount = await _context.WarrantyClaims
                .CountAsync(w => w.Status == "Active" || w.Status == "Processing");

            // Recent Activity
            var recentStockIns = await _context.StockIns
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

            var recentStockOuts = await _context.StockOuts
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
