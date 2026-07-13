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
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            
            StockAdjustment? existing = null;
            if (adjustment.Id > 0)
            {
                existing = db.StockAdjustments
                    .Include(s => s.Lines)
                    .FirstOrDefault(s => s.Id == adjustment.Id);
            }

            if (existing != null)
            {
                var lifecycle = new StockDocumentLifecycleService();
                lifecycle.EnsureCanEditDetails(ParseStatus(existing.Status));

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
                adjustment.Status = DocumentStatus.Draft;

                if (string.IsNullOrWhiteSpace(adjustment.DocumentCode))
                {
                    adjustment.DocumentCode = $"ADJ-{DateTime.Now:yyyyMMddHHmmss}";
                }

                db.StockAdjustments.Add(adjustment);
                db.SaveChanges();
            }
        }

        public virtual void SubmitForApproval(int adjustmentId, int userId)
        {
            using var db = _contextFactory();
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var adjustment = db.StockAdjustments.SingleOrDefault(item => item.Id == adjustmentId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu điều chỉnh.");
            var lifecycle = new StockDocumentLifecycleService();
            adjustment.Status = lifecycle.SubmitForApproval(ParseStatus(adjustment.Status)).ToString();
            db.SaveChanges();
        }

        public virtual void Approve(int adjustmentId, int userId)
        {
            using var db = _contextFactory();
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var adjustment = db.StockAdjustments.SingleOrDefault(item => item.Id == adjustmentId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu điều chỉnh.");
            var lifecycle = new StockDocumentLifecycleService();
            adjustment.Status = lifecycle.Approve(
                ParseStatus(adjustment.Status),
                AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock)).ToString();
            adjustment.ApprovedBy = userId;
            adjustment.ApprovedAt = DateTime.UtcNow;
            db.SaveChanges();
        }

        public void Post(int adjustmentId, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);

            var adjustment = db.StockAdjustments
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.Id == adjustmentId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu điều chỉnh.");
            var lifecycle = new StockDocumentLifecycleService();
            lifecycle.EnsureCanPost(ParseStatus(adjustment.Status));
            if (!AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock))
            {
                throw new InventoryDomainException("You are not authorized to approve stock documents.");
            }

            if (adjustment.Lines.Count == 0)
            {
                throw new InventoryDomainException("Phiếu điều chỉnh phải có ít nhất một dòng hàng.");
            }

            adjustment.PostedBy = userId;
            adjustment.PostedAt = DateTime.UtcNow;
            db.SaveChanges();

            var postingService = new InventoryAdjustmentService(
                new EfInventoryUnitOfWork(db),
                new FixedWarehouseProvider(adjustment.WarehouseId),
                new SystemClock());
            postingService.PostAdjustment(new PostStockAdjustmentCommand(
                adjustment.Id,
                StockDocumentStatus.Approved,
                adjustment.ReferenceDocumentCode ?? string.Empty,
                adjustment.ReasonCode ?? string.Empty,
                BuildLineCommands(db, adjustment.Lines),
                userId));

            transaction.Commit();
        }

        private static IReadOnlyCollection<StockAdjustmentLineCommand> BuildLineCommands(
            AppDbContext db,
            IEnumerable<StockAdjustmentLine> lines)
        {
            return lines.Select(line =>
            {
                var serialNumbers = StockInService.ParseSerialRange(line.DraftSerials ?? string.Empty)
                    .Select(serial => serial.Trim())
                    .Where(serial => serial.Length > 0)
                    .ToList();
                if (line.ProductSerialId.HasValue && serialNumbers.Count == 0)
                {
                    var serial = db.ProductSerials.Find(line.ProductSerialId.Value)
                        ?? throw new InventoryDomainException($"Serial id {line.ProductSerialId.Value} does not exist.");
                    serialNumbers.Add(serial.SerialNumber);
                }

                return new StockAdjustmentLineCommand(
                    line.ProductId,
                    ParseDirection(line.Direction),
                    Math.Abs(line.BaseQuantityDelta),
                    serialNumbers);
            }).ToArray();
        }

        private static StockDocumentStatus ParseStatus(string status)
        {
            if (status == "nháp" || status == DocumentStatus.Draft) return StockDocumentStatus.Draft;
            if (status == "đã ghi sổ" || status == DocumentStatus.Posted) return StockDocumentStatus.Posted;

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
