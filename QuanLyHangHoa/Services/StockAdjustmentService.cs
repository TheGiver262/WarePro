using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockAdjustmentService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockAdjustmentService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public virtual List<StockAdjustment> GetAll()
        {
            using var db = _contextFactory();
            return db.StockAdjustments.AsNoTracking()
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .OrderByDescending(s => s.PostedAt ?? DateTime.MinValue)
                .ThenByDescending(s => s.Id)
                .ToList();
        }

        public virtual StockAdjustment? GetById(int id)
        {
            using var db = _contextFactory();
            return db.StockAdjustments.AsNoTracking()
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.ProductSerial)
                .FirstOrDefault(s => s.Id == id);
        }

        public virtual void SaveDraft(StockAdjustment adjustment, List<StockAdjustmentLine> lines, int userId)
        {
            using var db = _contextFactory();
            
            StockAdjustment? existing = null;
            if (adjustment.Id > 0)
            {
                existing = db.StockAdjustments
                    .Include(s => s.Lines)
                    .FirstOrDefault(s => s.Id == adjustment.Id);
            }

            if (existing != null)
            {
                if (existing.Status == "Posted") throw new Exception("Không thể cập nhật phiếu đã ghi sổ.");

                // Update header
                existing.WarehouseId = adjustment.WarehouseId;
                existing.AdjustmentType = adjustment.AdjustmentType;
                existing.ReasonCode = adjustment.ReasonCode;
                existing.Notes = adjustment.Notes;
                existing.ReferenceDocumentCode = adjustment.ReferenceDocumentCode;
                existing.ReferenceDocumentType = adjustment.ReferenceDocumentType;
                existing.ReferenceDocumentId = adjustment.ReferenceDocumentId;

                // Sync lines
                db.StockAdjustmentLines.RemoveRange(existing.Lines);
                foreach (var line in lines)
                {
                    line.AdjustmentId = existing.Id;
                    db.StockAdjustmentLines.Add(line);
                }
                
                db.SaveChanges();
                adjustment.Id = existing.Id;
                adjustment.Status = existing.Status;
            }
            else
            {
                adjustment.Lines = lines;
                adjustment.CreatedBy = userId;
                adjustment.Status = "Draft";

                if (string.IsNullOrWhiteSpace(adjustment.DocumentCode))
                {
                    adjustment.DocumentCode = $"ADJ-{DateTime.Now:yyyyMMddHHmmss}";
                }

                db.StockAdjustments.Add(adjustment);
                db.SaveChanges();
            }
        }

        public void Post(int adjustmentId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();

            var adjustment = db.StockAdjustments
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == adjustmentId);

            if (adjustment == null) throw new Exception("Không tìm thấy phiếu điều chỉnh.");
            if (adjustment.Status == "Posted") throw new Exception("Phiếu này đã được ghi sổ.");

            adjustment.Status = "Posted";
            adjustment.PostedBy = userId;
            adjustment.PostedAt = DateTime.Now;
            db.SaveChanges();

            var postingService = new InventoryAdjustmentService(
                new EfInventoryUnitOfWork(db),
                new FixedWarehouseProvider(adjustment.WarehouseId),
                new SystemClock());

            postingService.PostAdjustment(new PostStockAdjustmentCommand(
                adjustment.Id,
                ParseStatus(adjustment.Status),
                adjustment.ReferenceDocumentCode ?? string.Empty,
                adjustment.ReasonCode ?? string.Empty,
                BuildLineCommands(db, adjustment.Lines ?? Enumerable.Empty<StockAdjustmentLine>()),
                userId));

            transaction.Commit();
        }

        private static IReadOnlyCollection<StockAdjustmentLineCommand> BuildLineCommands(
            AppDbContext db,
            IEnumerable<StockAdjustmentLine> lines)
        {
            return lines.Select(line =>
            {
                var serialNumbers = Array.Empty<string>();
                if (line.ProductSerialId.HasValue)
                {
                    var serial = db.ProductSerials.Find(line.ProductSerialId.Value)
                        ?? throw new InventoryDomainException($"Serial id {line.ProductSerialId.Value} does not exist.");
                    serialNumbers = new[] { serial.SerialNumber };
                }

                return new StockAdjustmentLineCommand(
                    line.ProductId,
                    ParseDirection(line.Direction),
                    (int)Math.Abs(line.BaseQuantityDelta),
                    serialNumbers);
            }).ToArray();
        }

        private static StockDocumentStatus ParseStatus(string status)
        {
            if (status == "Draft") return StockDocumentStatus.Draft;

            return Enum.TryParse<StockDocumentStatus>(status, out var parsed)
                ? parsed
                : throw new InventoryDomainException($"Unsupported stock adjustment status {status}.");
        }

        private static StockLedgerDirection ParseDirection(string direction)
        {
            return Enum.TryParse<StockLedgerDirection>(direction, out var parsed)
                ? parsed
                : throw new InventoryDomainException($"Unsupported stock adjustment direction {direction}.");
        }

        private sealed class FixedWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly int _warehouseId;

            public FixedWarehouseProvider(int warehouseId)
            {
                _warehouseId = warehouseId;
            }

            public int GetDefaultWarehouseId()
            {
                return _warehouseId;
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
