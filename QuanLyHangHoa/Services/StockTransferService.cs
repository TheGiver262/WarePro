using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            
            // Map transient ProductSerial objects to database-tracked ones to avoid UNIQUE KEY constraint errors
            foreach (var line in lines)
            {
                var serials = line.ProductSerials.Select(ps => ps.SerialNumber).ToList();
                if (serials.Any())
                {
                    var dbSerials = db.ProductSerials
                        .Where(ps => ps.ProductId == line.ProductId && serials.Contains(ps.SerialNumber))
                        .ToList();
                    line.ProductSerials = dbSerials;
                }
                else
                {
                    line.ProductSerials = new List<ProductSerial>();
                }
            }

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

                var beforeJson = Serialize(existing);

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

                var afterJson = Serialize(existing);
                AddAudit(db, "UPDATE", existing.Id, beforeJson, afterJson, userId);
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

                var afterJson = Serialize(stockTransfer);
                AddAudit(db, "CREATE", stockTransfer.Id, null, afterJson, userId);
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

            var beforeJson = Serialize(stockTransfer);

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

            var afterJson = Serialize(stockTransfer);
            AddAudit(db, "UPDATE", stockTransfer.Id, beforeJson, afterJson, userId);

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

        public virtual void Delete(int id, int userId)
        {
            using var db = _contextFactory();
            var stockTransfer = db.StockTransfers
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == id);

            if (stockTransfer == null) throw new Exception("Không tìm thấy phiếu chuyển kho.");
            if (stockTransfer.Status == "Posted")
                throw new Exception("Không thể xóa phiếu đã ghi sổ.");

            var beforeJson = JsonSerializer.Serialize(new { stockTransfer.Id, stockTransfer.DocumentCode });

            db.StockTransferLines.RemoveRange(stockTransfer.Lines);
            db.StockTransfers.Remove(stockTransfer);
            db.SaveChanges();

            AddAudit(db, "DELETE", id, beforeJson, null, userId);
        }

        private string Serialize(StockTransfer s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.DocumentCode,
                s.FromWarehouseId,
                s.ToWarehouseId,
                s.TransferDate,
                s.Notes,
                s.Status,
                s.CreatedAt,
                s.CreatedBy,
                s.UpdatedAt,
                s.UpdatedBy,
                s.PostedAt,
                s.PostedBy,
                Lines = s.Lines?.Select(l => new
                {
                    l.Id,
                    l.ProductId,
                    l.Quantity,
                    l.UnitId,
                    Serials = l.ProductSerials == null ? null :
                              (l.ProductSerials.Count > 10 ? l.ProductSerials.Take(10).Select(ps => ps.SerialNumber).Concat(new[] { $"... and {l.ProductSerials.Count - 10} more" }).ToList()
                                                           : l.ProductSerials.Select(ps => ps.SerialNumber).ToList())
                }).ToList()
            });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockTransfer",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            db.SaveChanges();
        }
    }
}
