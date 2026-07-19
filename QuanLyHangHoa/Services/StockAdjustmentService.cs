using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý lifecycle phiếu điều chỉnh và chuyển line đã duyệt sang InventoryAdjustmentService để ghi tồn.
    /// </summary>
    public class StockAdjustmentService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public StockAdjustmentService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
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

        // draft có thể thay toàn bộ line; lifecycle chặn sửa khi đã submit hoặc post.
        public async Task SaveDraftAsync(
            StockAdjustment adjustment,
            List<StockAdjustmentLine> lines,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(adjustment);
            ArgumentNullException.ThrowIfNull(lines);
            var timestamp = DateTime.Now;
            // snapshot giữ nguyên cả liên kết chứng từ nguồn; retry không được đổi lý do hoặc nguồn điều chỉnh giữa chừng.
            var snapshot = new SaveDraftSnapshot(
                adjustment.Id,
                string.IsNullOrWhiteSpace(adjustment.DocumentCode)
                    ? $"ADJ-{timestamp:yyyyMMddHHmmss}"
                    : adjustment.DocumentCode,
                adjustment.WarehouseId,
                adjustment.AdjustmentType,
                adjustment.ReasonCode,
                adjustment.Notes,
                adjustment.ReferenceDocumentCode,
                adjustment.ReferenceDocumentType,
                adjustment.ReferenceDocumentId,
                adjustment.RowVersion.ToArray(),
                lines.Select(line => new SaveDraftLineSnapshot(
                    line.ProductId,
                    line.ProductSerialId,
                    line.DraftSerials,
                    line.QuantityDelta,
                    line.BaseQuantityDelta,
                    line.Direction)).ToArray());
            if (snapshot.Id > 0 && snapshot.RowVersion.Length == 0)
            {
                throw new ArgumentException("RowVersion is required for draft updates.", nameof(adjustment));
            }

            var savedId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-adjustment.save-draft", operationId),
                (db, token) => StageSaveDraftAsync(db, snapshot, userId, token),
                entityKey: snapshot.DocumentCode,
                cancellationToken: cancellationToken);
            adjustment.Id = savedId;
            adjustment.DocumentCode = snapshot.DocumentCode;
        }

        internal virtual void SaveDraft(StockAdjustment adjustment, List<StockAdjustmentLine> lines, int userId) =>
            SaveDraftAsync(adjustment, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private async Task<int> StageSaveDraftAsync(
            AppDbContext db,
            SaveDraftSnapshot snapshot,
            int userId,
            CancellationToken cancellationToken)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);

            StockAdjustment? existing = null;
            if (snapshot.Id > 0)
            {
                existing = db.StockAdjustments
                    .Include(item => item.Lines)
                    .FirstOrDefault(item => item.Id == snapshot.Id);
            }

            var freshLines = snapshot.Lines.Select(line => line.ToEntity()).ToList();
            if (existing is not null)
            {
                new StockDocumentLifecycleService().EnsureCanEditDetails(ParseStatus(existing.Status));
                // rowversion biến thao tác thay toàn bộ line thành cập nhật có điều kiện, không âm thầm đè bản mới hơn.
                db.Entry(existing).Property(item => item.RowVersion).OriginalValue = snapshot.RowVersion;
                db.Entry(existing).Property(item => item.Notes).IsModified = true;

                existing.WarehouseId = snapshot.WarehouseId;
                existing.AdjustmentType = snapshot.AdjustmentType;
                existing.ReasonCode = snapshot.ReasonCode;
                existing.Notes = snapshot.Notes;
                existing.ReferenceDocumentCode = snapshot.ReferenceDocumentCode;
                existing.ReferenceDocumentType = snapshot.ReferenceDocumentType;
                existing.ReferenceDocumentId = snapshot.ReferenceDocumentId;
                db.StockAdjustmentLines.RemoveRange(existing.Lines);
                existing.Lines = freshLines;
                return existing.Id;
            }

            var freshAdjustment = new StockAdjustment
            {
                DocumentCode = snapshot.DocumentCode,
                WarehouseId = snapshot.WarehouseId,
                AdjustmentType = snapshot.AdjustmentType,
                ReasonCode = snapshot.ReasonCode,
                Notes = snapshot.Notes,
                ReferenceDocumentCode = snapshot.ReferenceDocumentCode,
                ReferenceDocumentType = snapshot.ReferenceDocumentType,
                ReferenceDocumentId = snapshot.ReferenceDocumentId,
                CreatedBy = userId,
                Status = DocumentStatus.Draft,
                Lines = freshLines
            };
            db.StockAdjustments.Add(freshAdjustment);
            // flush để database cấp id phiếu trước khi trả về; transaction ngoài vẫn quyết định commit hay rollback.
            await db.SaveChangesAsync(cancellationToken);
            return freshAdjustment.Id;
        }

        private sealed record SaveDraftSnapshot(
            int Id,
            string DocumentCode,
            int WarehouseId,
            string AdjustmentType,
            string ReasonCode,
            string? Notes,
            string? ReferenceDocumentCode,
            string? ReferenceDocumentType,
            int? ReferenceDocumentId,
            byte[] RowVersion,
            SaveDraftLineSnapshot[] Lines);

        private sealed record SaveDraftLineSnapshot(
            int ProductId,
            int? ProductSerialId,
            string? DraftSerials,
            decimal QuantityDelta,
            decimal BaseQuantityDelta,
            string Direction)
        {
            public StockAdjustmentLine ToEntity() => new()
            {
                ProductId = ProductId,
                ProductSerialId = ProductSerialId,
                DraftSerials = DraftSerials,
                QuantityDelta = QuantityDelta,
                BaseQuantityDelta = BaseQuantityDelta,
                Direction = Direction
            };
        }
        public Task SubmitForApprovalAsync(
            int adjustmentId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-adjustment.submit", operationId),
                (db, token) => StageSubmitForApprovalAsync(db, adjustmentId, userId),
                (db, token) => db.StockAdjustments.AnyAsync(
                    item => item.Id == adjustmentId && item.Status == DocumentStatus.PendingApproval, token),
                cancellationToken: cancellationToken);

        internal virtual void SubmitForApproval(int adjustmentId, int userId) =>
            SubmitForApprovalAsync(adjustmentId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageSubmitForApprovalAsync(AppDbContext db, int adjustmentId, int userId)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var adjustment = db.StockAdjustments.SingleOrDefault(item => item.Id == adjustmentId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu điều chỉnh.");
            var lifecycle = new StockDocumentLifecycleService();
            adjustment.Status = lifecycle.SubmitForApproval(ParseStatus(adjustment.Status)).ToString();
            return Task.CompletedTask;
        }

        public Task ApproveAsync(
            int adjustmentId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-adjustment.approve", operationId),
                (db, token) => StageApproveAsync(db, adjustmentId, userId),
                (db, token) => db.StockAdjustments.AnyAsync(
                    item => item.Id == adjustmentId && item.Status == DocumentStatus.Approved, token),
                cancellationToken: cancellationToken);

        internal virtual void Approve(int adjustmentId, int userId) =>
            ApproveAsync(adjustmentId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageApproveAsync(AppDbContext db, int adjustmentId, int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var adjustment = db.StockAdjustments.SingleOrDefault(item => item.Id == adjustmentId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu điều chỉnh.");
            var lifecycle = new StockDocumentLifecycleService();
            adjustment.Status = lifecycle.Approve(
                ParseStatus(adjustment.Status),
                AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock)).ToString();
            adjustment.ApprovedBy = userId;
            adjustment.ApprovedAt = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task PostAsync(
            int adjustmentId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-adjustment.post", operationId),
                (db, token) => StagePostAsync(db, adjustmentId, userId),
                (db, token) => db.StockAdjustments.AnyAsync(
                    item => item.Id == adjustmentId && item.Status == DocumentStatus.Posted, token),
                cancellationToken: cancellationToken);

        internal void Post(int adjustmentId, int userId) =>
            PostAsync(adjustmentId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StagePostAsync(AppDbContext db, int adjustmentId, int userId)
        {
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

            // SaveChanges này vẫn nằm trong transaction và sẽ rollback nếu validation/posting line thất bại.
            adjustment.PostedBy = userId;
            adjustment.PostedAt = DateTime.UtcNow;

            var postingService = new InventoryAdjustmentService(
                new EfInventoryUnitOfWork(db, commitChanges: false),
                new FixedWarehouseProvider(adjustment.WarehouseId),
                new SystemClock());
            // gửi toàn bộ line qua domain service; mọi thay đổi chỉ được commit khi tất cả line xử lý thành công.
            postingService.PostAdjustment(new PostStockAdjustmentCommand(
                adjustment.Id,
                StockDocumentStatus.Approved,
                adjustment.ReferenceDocumentCode ?? string.Empty,
                adjustment.ReasonCode ?? string.Empty,
                BuildLineCommands(db, adjustment.Lines),
                userId));

            return Task.CompletedTask;
        }

        // quantity command luôn dương; Direction là nguồn quyết định tăng/giảm trong domain service.
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
                // dòng legacy có FK serial nhưng chưa có DraftSerials được chuyển lại thành serial number.
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
