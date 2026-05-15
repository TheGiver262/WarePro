using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockTransferService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockTransferService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public virtual List<StockTransfer> GetAll()
        {
            using var db = _contextFactory();
            return db.StockTransfers.AsNoTracking()
                .Include(s => s.FromWarehouse)
                .Include(s => s.ToWarehouse)
                .Include(s => s.Creator)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .OrderByDescending(s => s.CreatedAt)
                .ToList();
        }

        public virtual StockTransfer? GetById(int id)
        {
            using var db = _contextFactory();
            return db.StockTransfers.AsNoTracking()
                .Include(s => s.FromWarehouse)
                .Include(s => s.ToWarehouse)
                .Include(s => s.Creator)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerials)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Unit)
                .FirstOrDefault(s => s.Id == id);
        }

        public virtual void SaveDraft(StockTransfer stockTransfer, List<StockTransferLine> lines, int userId)
        {
            using var db = _contextFactory();
            
            StockTransfer? existing = null;
            if (stockTransfer.Id > 0)
            {
                existing = db.StockTransfers
                    .Include(s => s.Lines)
                        .ThenInclude(l => l.ProductSerials)
                    .FirstOrDefault(s => s.Id == stockTransfer.Id);
            }

            if (existing != null)
            {
                if (existing.Status == "Posted") throw new Exception("Không thể cập nhật phiếu đã ghi sổ.");

                // Update properties
                existing.FromWarehouseId = stockTransfer.FromWarehouseId;
                existing.ToWarehouseId = stockTransfer.ToWarehouseId;
                existing.TransferDate = stockTransfer.TransferDate;
                existing.Notes = stockTransfer.Notes;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = userId;

                // Simple strategy: Remove old lines and add new ones
                db.StockTransferLines.RemoveRange(existing.Lines);
                existing.Lines = lines;
                
                db.SaveChanges();
                stockTransfer.Id = existing.Id;
                stockTransfer.Status = existing.Status;
            }
            else
            {
                stockTransfer.Lines = lines;
                stockTransfer.CreatedBy = userId;
                stockTransfer.CreatedAt = DateTime.Now;
                stockTransfer.Status = "Draft";

                if (string.IsNullOrWhiteSpace(stockTransfer.DocumentCode))
                {
                    stockTransfer.DocumentCode = $"ST-{DateTime.Now:yyyyMMddHHmmss}";
                }

                db.StockTransfers.Add(stockTransfer);
                db.SaveChanges();
            }
        }

        public virtual void Post(int stockTransferId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            var stockTransfer = db.StockTransfers
                .Include(s => s.Lines)
                    .ThenInclude(l => l.ProductSerials)
                .FirstOrDefault(s => s.Id == stockTransferId);

            if (stockTransfer == null) throw new Exception("Không tìm thấy phiếu chuyển kho.");
            if (stockTransfer.Status == "Posted") throw new Exception("Phiếu này đã được ghi sổ.");
            if (stockTransfer.FromWarehouseId == stockTransfer.ToWarehouseId) throw new Exception("Kho đi và kho đến phải khác nhau.");

            // Validate serials before posting
            foreach (var line in stockTransfer.Lines)
            {
                var product = db.Products.Find(line.ProductId);
                if (product != null && product.IsSerialTracked)
                {
                    if (line.ProductSerials.Count != (int)line.Quantity)
                    {
                        throw new Exception($"Sản phẩm {product.DisplayName} yêu cầu {(int)line.Quantity} serial, nhưng hiện có {line.ProductSerials.Count}.");
                    }
                }
            }

            stockTransfer.Status = "Posted";
            stockTransfer.PostedBy = userId;
            stockTransfer.PostedAt = DateTime.Now;
            db.SaveChanges();

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // Locking strategy: Order by ProductId to avoid deadlocks
            foreach (var line in stockTransfer.Lines.OrderBy(l => l.ProductId))
            {
                var serials = line.ProductSerials.Select(s => s.SerialNumber).ToArray();

                postingService.PostStockTransfer(new PostStockTransferCommand(
                    stockTransfer.Id,
                    stockTransfer.FromWarehouseId,
                    stockTransfer.ToWarehouseId,
                    StockDocumentStatus.Posted,
                    line.ProductId,
                    (int)line.Quantity,
                    serials,
                    userId));
            }

            transaction.Commit();
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
