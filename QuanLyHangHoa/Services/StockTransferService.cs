using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý lifecycle chuyển kho và giữ việc trừ nguồn, cộng đích, đổi vị trí serial trong một transaction.
    /// </summary>
    public class StockTransferService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public StockTransferService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
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

        public async Task SaveDraftAsync(
            StockTransfer stockTransfer, List<StockTransferLine> lines, int userId,
            Guid operationId, CancellationToken cancellationToken = default)
        {
            var timestamp = DateTime.Now;
            // snapshot tách dữ liệu UI khỏi lần retry; mỗi dbcontext mới sẽ dựng lại graph chuyển kho từ các giá trị này.
            var snapshot = new SaveDraftSnapshot(
                stockTransfer.Id,
                string.IsNullOrWhiteSpace(stockTransfer.DocumentCode) ? $"ST-{timestamp:yyyyMMddHHmmss}" : stockTransfer.DocumentCode,
                stockTransfer.FromWarehouseId,
                stockTransfer.ToWarehouseId,
                stockTransfer.TransferDate,
                stockTransfer.Notes,
                stockTransfer.RowVersion.ToArray(),
                lines.Select(line => new SaveDraftLineSnapshot(
                    line.ProductId,
                    line.UnitId,
                    line.Quantity,
                    line.BaseQuantity,
                    line.ProductSerials.Select(serial => serial.SerialNumber).ToArray())).ToArray(),
                timestamp);
            if (snapshot.Id > 0 && snapshot.RowVersion.Length == 0)
            {
                throw new ArgumentException("RowVersion is required for draft updates.", nameof(stockTransfer));
            }

            var savedId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.save-draft", operationId),
                (db, token) => StageSaveDraftAsync(db, snapshot, userId, token),
                entityKey: snapshot.DocumentCode,
                cancellationToken: cancellationToken);
            stockTransfer.Id = savedId;
            stockTransfer.DocumentCode = snapshot.DocumentCode;
            stockTransfer.RowVersion = await LoadRowVersionAsync(savedId, cancellationToken);
        }

        internal virtual void SaveDraft(StockTransfer stockTransfer, List<StockTransferLine> lines, int userId) =>
            SaveDraftAsync(stockTransfer, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private async Task<int> StageSaveDraftAsync(
            AppDbContext db,
            SaveDraftSnapshot snapshot,
            int userId,
            CancellationToken cancellationToken)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            // nạp lại serial bằng dbcontext hiện tại để không gắn entity đang được context cũ theo dõi vào lần retry.
            var freshLines = BuildStockTransfer(db, snapshot.Lines);
            StockTransfer? existing = null;
            if (snapshot.Id > 0)
            {
                existing = db.StockTransfers
                    .Include(item => item.Lines)
                    .ThenInclude(line => line.ProductSerials)
                    .FirstOrDefault(item => item.Id == snapshot.Id);
            }

            if (snapshot.Id > 0 && existing is null)
            {
                throw new InventoryDomainException("Không tìm thấy chứng từ hoặc chứng từ đã bị xóa.");
            }

            if (existing is not null)
            {
                new StockDocumentLifecycleService().EnsureCanEditDetails(ParseStatus(existing.Status));
                var beforeJson = Serialize(existing);
                // dùng rowversion đã đọc làm điều kiện cập nhật, tránh ghi đè draft vừa được client khác lưu.
                db.Entry(existing).Property(item => item.RowVersion).OriginalValue = snapshot.RowVersion;

                existing.FromWarehouseId = snapshot.FromWarehouseId;
                existing.ToWarehouseId = snapshot.ToWarehouseId;
                existing.TransferDate = snapshot.TransferDate;
                existing.Notes = snapshot.Notes;
                existing.UpdatedAt = snapshot.Timestamp;
                existing.UpdatedBy = userId;
                db.StockTransferLines.RemoveRange(existing.Lines);
                existing.Lines = freshLines;
                AddAudit(db, "UPDATE", existing.Id, beforeJson, Serialize(existing), userId);
                return existing.Id;
            }

            var freshStockTransfer = new StockTransfer
            {
                DocumentCode = snapshot.DocumentCode,
                FromWarehouseId = snapshot.FromWarehouseId,
                ToWarehouseId = snapshot.ToWarehouseId,
                TransferDate = snapshot.TransferDate,
                Notes = snapshot.Notes,
                Status = DocumentStatus.Draft,
                CreatedBy = userId,
                CreatedAt = snapshot.Timestamp,
                Lines = freshLines
            };
            db.StockTransfers.Add(freshStockTransfer);
            // flush để có id chứng từ trước khi tạo bản audit tham chiếu đến chứng từ mới.
            await db.SaveChangesAsync(cancellationToken);
            AddAudit(db, "CREATE", freshStockTransfer.Id, null, Serialize(freshStockTransfer), userId);
            return freshStockTransfer.Id;
        }

        private static List<StockTransferLine> BuildStockTransfer(
            AppDbContext db,
            IReadOnlyList<SaveDraftLineSnapshot> snapshots) =>
            snapshots.Select(line => new StockTransferLine
            {
                ProductId = line.ProductId,
                UnitId = line.UnitId,
                Quantity = line.Quantity,
                BaseQuantity = line.BaseQuantity,
                ProductSerials = line.SerialNumbers.Length == 0
                    ? new List<ProductSerial>()
                    : db.ProductSerials.Where(serial =>
                        serial.ProductId == line.ProductId && line.SerialNumbers.Contains(serial.SerialNumber)).ToList()
            }).ToList();

        private sealed record SaveDraftSnapshot(
            int Id,
            string DocumentCode,
            int FromWarehouseId,
            int ToWarehouseId,
            DateTime TransferDate,
            string? Notes,
            byte[] RowVersion,
            SaveDraftLineSnapshot[] Lines,
            DateTime Timestamp);

        private sealed record SaveDraftLineSnapshot(
            int ProductId,
            int UnitId,
            decimal Quantity,
            decimal BaseQuantity,
            string[] SerialNumbers);

        public Task SubmitForApprovalAsync(
            int stockTransferId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.submit", operationId),
                (db, token) => StageSubmitForApprovalAsync(db, stockTransferId, userId),
                (db, token) => db.StockTransfers.AnyAsync(
                    item => item.Id == stockTransferId && item.Status == DocumentStatus.PendingApproval, token),
                cancellationToken: cancellationToken);

        public async Task<byte[]> SubmitForApprovalAsync(
            int stockTransferId, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.submit", operationId),
                (db, token) => StageSubmitForApprovalAsync(db, stockTransferId, rowVersion, userId),
                (db, token) => db.StockTransfers.AnyAsync(
                    item => item.Id == stockTransferId && item.Status == DocumentStatus.PendingApproval, token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockTransferId, cancellationToken);
        }

        internal virtual void SubmitForApproval(int stockTransferId, int userId) =>
            SubmitForApprovalAsync(stockTransferId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageSubmitForApprovalAsync(AppDbContext db, int stockTransferId, int userId) =>
            StageSubmitForApprovalAsync(db, stockTransferId, null, userId);

        private Task StageSubmitForApprovalAsync(
            AppDbContext db,
            int stockTransferId,
            byte[]? expectedRowVersion,
            int userId)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var transfer = db.StockTransfers.SingleOrDefault(item => item.Id == stockTransferId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu chuyển kho.");
            var beforeJson = Serialize(transfer);
            if (expectedRowVersion is not null)
            {
                db.Entry(transfer).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            }
            var lifecycle = new StockDocumentLifecycleService();
            transfer.Status = lifecycle.SubmitForApproval(ParseStatus(transfer.Status)).ToString();
            transfer.UpdatedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;
            AddAudit(db, "SUBMIT", transfer.Id, beforeJson, Serialize(transfer), userId);
            return Task.CompletedTask;
        }

        public Task ApproveAsync(
            int stockTransferId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.approve", operationId),
                (db, token) => StageApproveAsync(db, stockTransferId, userId),
                (db, token) => db.StockTransfers.AnyAsync(
                    item => item.Id == stockTransferId && item.Status == DocumentStatus.Approved, token),
                cancellationToken: cancellationToken);

        public async Task<byte[]> ApproveAsync(
            int stockTransferId, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.approve", operationId),
                (db, token) => StageApproveAsync(db, stockTransferId, rowVersion, userId),
                (db, token) => db.StockTransfers.AnyAsync(
                    item => item.Id == stockTransferId && item.Status == DocumentStatus.Approved, token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockTransferId, cancellationToken);
        }

        internal virtual void Approve(int stockTransferId, int userId) =>
            ApproveAsync(stockTransferId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageApproveAsync(AppDbContext db, int stockTransferId, int userId) =>
            StageApproveAsync(db, stockTransferId, null, userId);

        private Task StageApproveAsync(
            AppDbContext db,
            int stockTransferId,
            byte[]? expectedRowVersion,
            int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var transfer = db.StockTransfers.SingleOrDefault(item => item.Id == stockTransferId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu chuyển kho.");
            var lifecycle = new StockDocumentLifecycleService();
            var beforeJson = Serialize(transfer);
            if (expectedRowVersion is not null)
            {
                db.Entry(transfer).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            }
            transfer.Status = lifecycle.Approve(
                ParseStatus(transfer.Status),
                AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock)).ToString();
            transfer.ApprovedBy = userId;
            transfer.ApprovedAt = DateTime.UtcNow;
            transfer.UpdatedBy = userId;
            transfer.UpdatedAt = DateTime.UtcNow;
            AddAudit(db, "APPROVE", transfer.Id, beforeJson, Serialize(transfer), userId);
            return Task.CompletedTask;
        }

        public Task PostAsync(
            int stockTransferId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.post", operationId),
                (db, token) => StagePostAsync(db, stockTransferId, userId),
                (db, token) => db.StockTransfers.AnyAsync(
                    item => item.Id == stockTransferId && item.Status == DocumentStatus.Posted, token),
                cancellationToken: cancellationToken);

        public async Task<byte[]> PostAsync(
            int stockTransferId, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.post", operationId),
                (db, token) => StagePostAsync(db, stockTransferId, rowVersion, userId),
                (db, token) => db.StockTransfers.AnyAsync(
                    item => item.Id == stockTransferId && item.Status == DocumentStatus.Posted, token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockTransferId, cancellationToken);
        }

        internal virtual void Post(int stockTransferId, int userId) =>
            PostAsync(stockTransferId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StagePostAsync(AppDbContext db, int stockTransferId, int userId) =>
            StagePostAsync(db, stockTransferId, null, userId);

        private Task StagePostAsync(
            AppDbContext db,
            int stockTransferId,
            byte[]? expectedRowVersion,
            int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);

            var stockTransfer = db.StockTransfers
                .Include(transfer => transfer.Lines)
                    .ThenInclude(line => line.ProductSerials)
                .FirstOrDefault(transfer => transfer.Id == stockTransferId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu chuyển kho.");
            if (expectedRowVersion is not null)
            {
                db.Entry(stockTransfer).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            }
            var lifecycle = new StockDocumentLifecycleService();
            lifecycle.EnsureCanPost(ParseStatus(stockTransfer.Status));
            if (!AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock))
            {
                throw new InventoryDomainException("You are not authorized to approve stock documents.");
            }

            if (stockTransfer.Lines.Count == 0)
            {
                throw new InventoryDomainException("Phiếu chuyển kho phải có ít nhất một dòng hàng.");
            }

            if (stockTransfer.FromWarehouseId == stockTransfer.ToWarehouseId)
            {
                throw new InventoryDomainException("Kho đi và kho đến phải khác nhau.");
            }

            // kiểm tra đủ serial theo BaseQuantity trước khi bất kỳ balance nào được lưu.
            var beforeJson = Serialize(stockTransfer);
            var productIds = stockTransfer.Lines
                .Select(line => line.ProductId)
                .Distinct()
                .ToList();
            var productMap = db.Products
                .Where(product => productIds.Contains(product.Id))
                .ToDictionary(product => product.Id);
            var balances = db.StockBalances
                .Where(balance =>
                    productIds.Contains(balance.ProductId) &&
                    (balance.WarehouseId == stockTransfer.FromWarehouseId ||
                     balance.WarehouseId == stockTransfer.ToWarehouseId))
                .ToList();
            var destinationProductIds = balances
                .Where(balance => balance.WarehouseId == stockTransfer.ToWarehouseId)
                .Select(balance => balance.ProductId)
                .ToHashSet();
            foreach (var productId in productMap.Keys.Where(
                         productId => !destinationProductIds.Contains(productId)))
            {
                db.StockBalances.Add(new StockBalance
                {
                    ProductId = productId,
                    WarehouseId = stockTransfer.ToWarehouseId
                });
            }
            var serialNumbers = stockTransfer.Lines
                .SelectMany(line => line.ProductSerials.Select(serial => serial.SerialNumber))
                .ToList();
            foreach (var line in stockTransfer.Lines)
            {
                if (!productMap.TryGetValue(line.ProductId, out var product))
                    throw new InventoryDomainException($"Product {line.ProductId} does not exist.");

                if (!product.IsSerialTracked)
                {
                    continue;
                }

                if (line.BaseQuantity != decimal.Truncate(line.BaseQuantity))
                {
                    throw new InventoryDomainException(
                        $"Sản phẩm {product.DisplayName} theo dõi serial nên số lượng cơ sở phải là số nguyên.");
                }

                var requiredSerialCount = (int)line.BaseQuantity;
                if (line.ProductSerials.Count != requiredSerialCount)
                {
                    throw new InventoryDomainException(
                        $"Sản phẩm {product.DisplayName} yêu cầu {requiredSerialCount} serial, nhưng hiện có {line.ProductSerials.Count}.");
                }
            }

            // trạng thái posting được lưu trong transaction và sẽ rollback nếu một line chuyển thất bại.
            stockTransfer.PostedBy = userId;
            stockTransfer.PostedAt = DateTime.UtcNow;

            var unitOfWork = new EfInventoryUnitOfWork(db, commitChanges: false);
            var transferSerialSet = serialNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var preloadedSerialEntities = db.ProductSerials.Local
                .Where(s => transferSerialSet.Contains(s.SerialNumber))
                .ToList();
            unitOfWork.MarkSerialsLoaded(preloadedSerialEntities);
            unitOfWork.MarkSerialsLoaded(serialNumbers);
            var postingService = new InventoryPostingService(
                unitOfWork,
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // thứ tự ProductId nhất quán giảm nguy cơ deadlock khi nhiều phiếu chạm cùng các balance.
            foreach (var line in stockTransfer.Lines.OrderBy(item => item.ProductId))
            {
                postingService.PostStockTransfer(new PostStockTransferCommand(
                    stockTransfer.Id,
                    stockTransfer.FromWarehouseId,
                    stockTransfer.ToWarehouseId,
                    StockDocumentStatus.Approved,
                    line.ProductId,
                    line.BaseQuantity,
                    line.ProductSerials.Select(serial => serial.SerialNumber).ToArray(),
                    userId));
            }

            AddAudit(db, "POST", stockTransfer.Id, beforeJson, Serialize(stockTransfer), userId);
            return Task.CompletedTask;
        }

        // hỗ trợ chuỗi legacy nhưng mọi transition phía trên đều dùng enum chuẩn.
        private static StockDocumentStatus ParseStatus(string status)
        {
            if (status == "nháp" || status == DocumentStatus.Draft)
            {
                return StockDocumentStatus.Draft;
            }

            if (status == "đã ghi sổ" || status == DocumentStatus.Posted)
            {
                return StockDocumentStatus.Posted;
            }

            return Enum.TryParse<StockDocumentStatus>(status, ignoreCase: true, out var parsed)
                ? parsed
                : throw new InventoryDomainException($"Unsupported stock transfer status {status}.");
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

        private async Task<byte[]> LoadRowVersionAsync(int id, CancellationToken cancellationToken)
        {
            await using var db = _contextFactory();
            return (await db.StockTransfers.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken))?.ToArray()
                ?? throw new InventoryDomainException("Không tìm thấy phiếu chuyển kho.");
        }

        private static byte[] SnapshotRowVersion(byte[] expectedRowVersion)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            if (expectedRowVersion.Length == 0)
            {
                throw new ArgumentException("RowVersion is required.", nameof(expectedRowVersion));
            }
            return expectedRowVersion.ToArray();
        }

        public Task DeleteAsync(
            int id,
            byte[] expectedRowVersion,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            if (expectedRowVersion.Length == 0)
            {
                throw new ArgumentException("RowVersion is required.", nameof(expectedRowVersion));
            }

            var rowVersion = expectedRowVersion.ToArray();
            return _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-transfer.delete", operationId),
                (db, token) => StageDeleteAsync(db, id, rowVersion, userId),
                entityKey: id.ToString(),
                cancellationToken: cancellationToken);
        }
        private Task StageDeleteAsync(AppDbContext db, int id, byte[] expectedRowVersion, int userId)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
            var stockTransfer = db.StockTransfers.Include(item => item.Lines).FirstOrDefault(item => item.Id == id)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu chuyển kho.");
            if (stockTransfer.Status != DocumentStatus.Draft)
            {
                throw new InventoryDomainException("Chỉ có thể xóa phiếu chuyển kho ở trạng thái nháp.");
            }

            db.Entry(stockTransfer).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;

            var beforeJson = JsonSerializer.Serialize(new { stockTransfer.Id, stockTransfer.DocumentCode });
            db.StockTransferLines.RemoveRange(stockTransfer.Lines);
            db.StockTransfers.Remove(stockTransfer);
            AddAudit(db, "DELETE", id, beforeJson, null, userId);
            return Task.CompletedTask;
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

        // audit POST dùng cùng transaction với phát sinh tồn kho; không tạo lịch sử thành công nếu transfer rollback.
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
        }
    }
}
