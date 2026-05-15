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

        public StockInService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public virtual List<StockIn> GetAll()
        {
            using var db = _contextFactory();
            return db.StockIns.AsNoTracking()
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerials)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public virtual StockIn? GetById(int id)
        {
            using var db = _contextFactory();
            return db.StockIns.AsNoTracking()
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerials)
                .FirstOrDefault(s => s.Id == id);
        }

        public virtual void SaveDraft(StockIn stockIn, List<StockInLine> lines, int userId)
        {
            using var db = _contextFactory();
            
            StockIn? existing = null;
            if (stockIn.Id > 0)
            {
                existing = db.StockIns
                    .Include(s => s.Lines)
                        .ThenInclude(l => l.ProductSerials)
                    .FirstOrDefault(s => s.Id == stockIn.Id);
            }

            if (existing != null)
            {
                if (existing.Status == "Posted") throw new Exception("Không thể cập nhật phiếu đã ghi sổ.");

                // Update properties
                existing.WarehouseId = stockIn.WarehouseId;
                existing.SupplierId = stockIn.SupplierId;
                existing.ImportDate = stockIn.ImportDate;
                existing.Notes = stockIn.Notes;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = userId;

                // Simple strategy: Remove old lines and add new ones
                // Or update matching lines. For simplicity in a draft, we can replace.
                db.StockInLines.RemoveRange(existing.Lines);
                existing.Lines = lines;
                
                db.SaveChanges();
                stockIn.Id = existing.Id;
                stockIn.Status = existing.Status;
            }
            else
            {
                stockIn.Lines = lines;
                stockIn.CreatedBy = userId;
                stockIn.CreatedAt = DateTime.Now;
                stockIn.Status = "Draft";

                if (string.IsNullOrWhiteSpace(stockIn.DocumentCode))
                {
                    stockIn.DocumentCode = $"SI-{DateTime.Now:yyyyMMddHHmmss}";
                }

                if (stockIn.WarehouseId == 0)
                {
                    stockIn.WarehouseId = new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId();
                }

                db.StockIns.Add(stockIn);
                db.SaveChanges();
            }
        }

        public virtual void Post(int stockInId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            var stockIn = db.StockIns
                .Include(s => s.Lines)
                    .ThenInclude(l => l.ProductSerials)
                .FirstOrDefault(s => s.Id == stockInId);

            if (stockIn == null) throw new Exception("Không tìm thấy phiếu nhập kho.");
            if (stockIn.Status == "Posted") throw new Exception("Phiếu này đã được ghi sổ.");

            // Validate serials before posting
            foreach (var line in stockIn.Lines)
            {
                var product = db.Products.Find(line.ProductId);
                if (product != null && product.IsSerialTracked)
                {
                    if (line.ProductSerials.Count != (int)line.Quantity)
                    {
                        throw new Exception($"Sản phẩm {product.DisplayName} yêu cầu { (int)line.Quantity } serial, nhưng hiện có {line.ProductSerials.Count}.");
                    }
                }
            }

            stockIn.Status = "Posted";
            stockIn.PostedBy = userId;
            stockIn.PostedAt = DateTime.Now;
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // Locking strategy: Order by ProductId to avoid deadlocks
            foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId))
            {
                var serials = line.ProductSerials.Select(s => s.SerialNumber).ToArray();

                postingService.PostStockIn(new PostStockInCommand(
                    stockIn.Id,
                    stockIn.WarehouseId,
                    StockInKind.Purchase,
                    StockDocumentStatus.Posted,
                    line.ProductId,
                    (int)line.Quantity,
                    serials,
                    userId));
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
                var rangeMatch = Regex.Match(trimmed, @"^(.+?)(\d+)-[^\d]*(\d+)$");
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
