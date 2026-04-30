using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockInService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockInService()
            : this(() => new AppDbContext())
        {
        }

        public StockInService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<StockIn> GetAll()
        {
            using var db = _contextFactory();
            return db.StockIns
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerials)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public void Create(StockIn stockIn, List<StockInLine> lines, int userId)
        {
            stockIn.Lines = lines;
            stockIn.CreatedBy = userId;
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            if (string.IsNullOrWhiteSpace(stockIn.DocumentCode))
            {
                stockIn.DocumentCode = $"SI-{DateTime.Now:yyyyMMddHHmmss}";
            }

            if (stockIn.WarehouseId == 0)
            {
                stockIn.WarehouseId = new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId();
            }

            var serialsByLine = stockIn.Lines.ToDictionary(
                line => line,
                line => line.ProductSerials?
                    .Select(serial => serial.SerialNumber)
                    .Where(serialNumber => !string.IsNullOrWhiteSpace(serialNumber))
                    .ToArray() ?? Array.Empty<string>());

            foreach (var line in stockIn.Lines)
            {
                line.ProductSerials?.Clear();
            }

            db.StockIns.Add(stockIn);
            db.SaveChanges();

            // Auto-post/approve
            stockIn.Status = "Posted";
            stockIn.PostedBy = stockIn.CreatedBy;
            stockIn.PostedAt = DateTime.UtcNow;
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId))
            {
                postingService.PostStockIn(new PostStockInCommand(
                    Guid.Empty,
                    stockIn.WarehouseId,
                    StockInKind.Purchase,
                    StockDocumentStatus.Posted,
                    line.ProductId,
                    (int)line.Quantity,
                    serialsByLine[line],
                    stockIn.PostedBy ?? stockIn.CreatedBy));
            }

            transaction.Commit();
        }

        public static List<string> ParseSerialRange(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return result;

            var parts = input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var rangeMatch = Regex.Match(trimmed, @"^([A-Za-z]*)(\d+)-[A-Za-z]*(\d+)$");
                if (rangeMatch.Success)
                {
                    string prefix = rangeMatch.Groups[1].Value;
                    string startStr = rangeMatch.Groups[2].Value;
                    string endStr   = rangeMatch.Groups[3].Value;
                    if (long.TryParse(startStr, out long start) && long.TryParse(endStr, out long end) && end >= start)
                    {
                        int padLen = startStr.Length;
                        for (long i = start; i <= end; i++)
                            result.Add(prefix + i.ToString().PadLeft(padLen, '0'));
                    }
                }
                else
                {
                    result.Add(trimmed);
                }
            }
            return result;
        }

        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;

            public DbDefaultWarehouseProvider(AppDbContext context)
            {
                _context = context;
            }

            public int GetDefaultWarehouseId()
            {
                var warehouseId = _context.Warehouses
                    .Where(warehouse => warehouse.IsDefault && warehouse.IsActive)
                    .Select(warehouse => warehouse.Id)
                    .FirstOrDefault();

                return warehouseId == 0 ? 1 : warehouseId;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
