using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockOutService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockOutService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public List<StockOut> GetAll()
        {
            using var db = _contextFactory();
            return db.StockOuts
                .Include(s => s.Customer)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public void Create(StockOut stockOut, List<StockOutLine> lines, int userId)
        {
            stockOut.Lines = lines;
            stockOut.CreatedBy = userId;
            stockOut.CreatedAt = DateTime.Now;
            
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            if (string.IsNullOrWhiteSpace(stockOut.DocumentCode))
            {
                stockOut.DocumentCode = $"SO-{DateTime.Now:yyyyMMddHHmmss}";
            }

            db.StockOuts.Add(stockOut);
            db.SaveChanges();

            // Auto-approve and post for now to match current simple logic
            stockOut.Status = "Posted";
            stockOut.PostedBy = userId;
            stockOut.PostedAt = DateTime.Now;
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // Sort lines by ProductId to prevent deadlocks
            foreach (var line in stockOut.Lines.OrderBy(l => l.ProductId))
            {
                var serials = line.ProductSerials?.Select(s => s.SerialNumber).ToArray() ?? Array.Empty<string>();
                
                postingService.PostStockOut(new PostStockOutCommand(
                    stockOut.Id,
                    stockOut.WarehouseId,
                    StockOutKind.Sale,
                    StockDocumentStatus.Posted,
                    line.ProductId,
                    (int)line.Quantity,
                    serials,
                    userId));
            }

            transaction.Commit();
        }

        public List<ProductSerial> GetInStockSerials(int productId, int warehouseId)
        {
            using var db = _contextFactory();
            return db.ProductSerials
                .Where(s => s.ProductId == productId && s.CurrentWarehouseId == warehouseId && s.CurrentStatus == "InStock")
                .ToList();
        }

        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;
            public DbDefaultWarehouseProvider(AppDbContext context) => _context = context;

            public int GetDefaultWarehouseId()
            {
                return _context.Warehouses
                    .Where(w => w.IsDefault && w.IsActive)
                    .Select(w => w.Id)
                    .FirstOrDefault() switch { 0 => 1, var id => id };
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
