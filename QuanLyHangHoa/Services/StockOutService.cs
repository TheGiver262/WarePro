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
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Services
{
    /// <summary>
    /// quản lý draft và lifecycle phiếu xuất; lúc post kiểm tra tồn/serial rồi ghi giảm tồn trong một transaction.
    /// </summary>
    public class StockOutService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public StockOutService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            _writeExecutor = new DatabaseWriteExecutor(contextFactory);
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

        public List<StockOut> GetStockOutPaged(
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.StockOuts.AsNoTracking()
                .Include(s => s.Customer)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .AsQueryable();

            query = ApplyStockOutFilters(query, code, customerName, startDate, endDate, warehouseId, status);

            return query
                .OrderByDescending(s => s.ExportDate)
                .ThenByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public DocumentListStats GetStockOutStats(
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status)
        {
            using var db = _contextFactory();
            var query = ApplyStockOutFilters(
                db.StockOuts.AsNoTracking(),
                code,
                customerName,
                startDate,
                endDate,
                warehouseId,
                status);

            return query
                .GroupBy(_ => 1)
                .Select(group => new DocumentListStats
                {
                    TotalCount = group.Count(),
                    DraftCount = group.Count(stockOut =>
                        stockOut.Status == DocumentStatus.Draft || stockOut.Status == "nháp"),
                    PendingApprovalCount = group.Count(stockOut =>
                        stockOut.Status == DocumentStatus.PendingApproval || stockOut.Status == "chờ duyệt"),
                    ApprovedCount = group.Count(stockOut =>
                        stockOut.Status == DocumentStatus.Approved || stockOut.Status == "đã duyệt"),
                    PostedCount = group.Count(stockOut =>
                        stockOut.Status == DocumentStatus.Posted || stockOut.Status == "đã ghi sổ")
                })
                .SingleOrDefault() ?? new DocumentListStats();
        }

        private IQueryable<StockOut> ApplyStockOutFilters(
            IQueryable<StockOut> query,
            string code,
            string customerName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                var term = code.Trim();
                query = query.Where(s => s.DocumentCode.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                var term = customerName.Trim();
                query = query.Where(s => s.Customer != null && s.Customer.DisplayName.Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(s => s.ExportDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.ExportDate <= endOfDay);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            var targetStatus = StockDocumentUiLifecycle.ParseFilter(status);
            if (targetStatus != null)
            {
                query = query.Where(s => s.Status == targetStatus
                    || (targetStatus == DocumentStatus.Draft && s.Status == "nháp")
                    || (targetStatus == DocumentStatus.PendingApproval && s.Status == "chờ duyệt")
                    || (targetStatus == DocumentStatus.Approved && s.Status == "đã duyệt")
                    || (targetStatus == DocumentStatus.Posted && s.Status == "đã ghi sổ"));
            }

            return query;
        }

        // API tiện ích này vẫn đi đủ Draft → PendingApproval → Approved → Posted, không bỏ qua lifecycle.
        public async Task CreateAsync(
            StockOut stockOut,
            List<StockOutLine> lines,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            await SaveDraftAsync(stockOut, lines, userId, operationId, cancellationToken);
            await SubmitForApprovalAsync(stockOut.Id, userId, operationId, cancellationToken);
            await ApproveAsync(stockOut.Id, userId, operationId, cancellationToken);
            await PostAsync(stockOut.Id, userId, operationId, cancellationToken);
        }

        internal void Create(StockOut stockOut, List<StockOutLine> lines, int userId) =>
            CreateAsync(stockOut, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        public async Task SaveDraftAsync(
            StockOut stockOut,
            List<StockOutLine> lines,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timestamp = DateTime.Now;
            if (stockOut.Id == 0 && string.IsNullOrWhiteSpace(stockOut.DocumentCode))
            {
                await using var numberingDb = _contextFactory();
                AuthorizationService.RequireFreshActor(numberingDb, userId, PermissionAction.PostStockOut);
                stockOut.DocumentCode = await DocumentNumberAllocator.AllocateAsync(
                    numberingDb,
                    "StockOut",
                    "OUT",
                    DateOnly.FromDateTime((stockOut.ExportDate ?? timestamp).Date),
                    cancellationToken);
            }
            // đóng băng cả header, line và serial trước khi vào executor để retry luôn dùng cùng một yêu cầu ban đầu.
            var snapshot = new SaveDraftSnapshot(
                stockOut.Id,
                stockOut.DocumentCode,
                stockOut.CustomerId,
                stockOut.WarehouseId,
                stockOut.PurposeCode,
                stockOut.ExportDate,
                stockOut.Notes,
                stockOut.StockCountSessionId,
                stockOut.StockCountLineId,
                stockOut.RowVersion.ToArray(),
                lines.Select(line => new SaveDraftLineSnapshot(
                    line.ProductId,
                    line.UnitId,
                    line.Quantity,
                    line.UnitPrice,
                    line.ProductSerials.Select(serial => serial.SerialNumber).ToArray())).ToArray(),
                timestamp);
            if (snapshot.Id > 0 && snapshot.RowVersion.Length == 0)
            {
                throw new ArgumentException("RowVersion is required for draft updates.", nameof(stockOut));
            }

            var savedId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.save-draft", operationId),
                (db, token) => StageSaveDraftAsync(db, snapshot, userId, token),
                entityKey: snapshot.DocumentCode,
                cancellationToken: cancellationToken);
            stockOut.Id = savedId;
            stockOut.DocumentCode = snapshot.DocumentCode;
            stockOut.RowVersion = await LoadRowVersionAsync(savedId, cancellationToken);
        }

        internal void SaveDraft(StockOut stockOut, List<StockOutLine> lines, int userId) =>
            SaveDraftAsync(stockOut, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private async Task<int> StageSaveDraftAsync(
            AppDbContext db,
            SaveDraftSnapshot snapshot,
            int userId,
            CancellationToken cancellationToken)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockOut);

            StockOut? existing = null;
            if (snapshot.Id > 0)
            {
                existing = db.StockOuts
                    .Include(item => item.Lines)
                    .FirstOrDefault(item => item.Id == snapshot.Id);
            }

            if (snapshot.Id > 0 && existing is null)
            {
                throw new InventoryDomainException("Không tìm thấy chứng từ hoặc chứng từ đã bị xóa.");
            }

            var productIds = snapshot.Lines.Select(line => line.ProductId).Distinct().ToList();
            var unitMap = db.ProductUnits.Where(unit => productIds.Contains(unit.ProductId)).ToList();
            var freshLines = BuildStockOut(snapshot.Lines, unitMap);

            if (existing is not null)
            {
                new StockDocumentLifecycleService().EnsureCanEditDetails(ParseStatus(existing.Status));
                var beforeJson = Serialize(existing);
                // rowversion từ màn hình là mốc so sánh; save sẽ thất bại nếu client khác đã cập nhật phiếu trước.
                db.Entry(existing).Property(item => item.RowVersion).OriginalValue = snapshot.RowVersion;

                existing.WarehouseId = snapshot.WarehouseId;
                existing.CustomerId = snapshot.CustomerId;
                existing.ExportDate = snapshot.ExportDate;
                existing.Notes = snapshot.Notes;
                existing.UpdatedAt = snapshot.Timestamp;
                existing.UpdatedBy = userId;
                db.StockOutLines.RemoveRange(existing.Lines);
                existing.Lines = freshLines;
                AddAudit(db, "UPDATE", existing.Id, beforeJson, Serialize(existing), userId);
                return existing.Id;
            }

            var freshStockOut = new StockOut
            {
                DocumentCode = snapshot.DocumentCode,
                CustomerId = snapshot.CustomerId,
                WarehouseId = snapshot.WarehouseId,
                PurposeCode = snapshot.PurposeCode,
                Status = DocumentStatus.Draft,
                ExportDate = snapshot.ExportDate,
                Notes = snapshot.Notes,
                CreatedBy = userId,
                CreatedAt = snapshot.Timestamp,
                // giữ khóa nguồn kiểm kê trên phiếu sinh tự động để unique index và truy vết chặn tạo điều chỉnh lặp.
                StockCountSessionId = snapshot.StockCountSessionId,
                StockCountLineId = snapshot.StockCountLineId,
                Lines = freshLines
            };
            db.StockOuts.Add(freshStockOut);
            // cần lấy id phiếu và line trước khi ghi audit hoặc dùng line làm mốc lịch sử serial.
            await db.SaveChangesAsync(cancellationToken);
            AddAudit(db, "CREATE", freshStockOut.Id, null, Serialize(freshStockOut), userId);
            return freshStockOut.Id;
        }

        private static List<StockOutLine> BuildStockOut(
            IReadOnlyList<SaveDraftLineSnapshot> snapshots,
            IReadOnlyList<ProductUnit> unitMap) =>
            snapshots.Select(line => new StockOutLine
            {
                ProductId = line.ProductId,
                UnitId = line.UnitId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                BaseQuantity = line.Quantity * (unitMap.FirstOrDefault(unit =>
                    unit.ProductId == line.ProductId && unit.UnitId == line.UnitId)?.ConversionFactor ?? 1m),
                DraftSerials = line.SerialNumbers.Length == 0
                    ? null
                    : string.Join(",", line.SerialNumbers.Select(serial => serial.Trim()).Where(serial => serial.Length > 0)),
                ProductSerials = new List<ProductSerial>()
            }).ToList();

        private sealed record SaveDraftSnapshot(
            int Id,
            string DocumentCode,
            int CustomerId,
            int WarehouseId,
            string PurposeCode,
            DateTime? ExportDate,
            string? Notes,
            int? StockCountSessionId,
            int? StockCountLineId,
            byte[] RowVersion,
            SaveDraftLineSnapshot[] Lines,
            DateTime Timestamp);

        private sealed record SaveDraftLineSnapshot(
            int ProductId,
            int UnitId,
            decimal Quantity,
            decimal UnitPrice,
            string[] SerialNumbers);

        public Task SubmitForApprovalAsync(
            int stockOutId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.submit", operationId),
                (db, token) => StageSubmitForApprovalAsync(db, stockOutId, userId, token),
                (db, token) => db.StockOuts.AnyAsync(
                    item => item.Id == stockOutId && item.Status == DocumentStatus.PendingApproval, token),
                cancellationToken: cancellationToken);

        public async Task<byte[]> SubmitForApprovalAsync(
            int stockOutId, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.submit", operationId),
                (db, token) => StageSubmitForApprovalAsync(db, stockOutId, rowVersion, userId, token),
                (db, token) => db.StockOuts.AnyAsync(
                    item => item.Id == stockOutId && item.Status == DocumentStatus.PendingApproval, token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockOutId, cancellationToken);
        }

        internal virtual void SubmitForApproval(int stockOutId, int userId) =>
            SubmitForApprovalAsync(stockOutId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageSubmitForApprovalAsync(
            AppDbContext db,
            int stockOutId,
            int userId,
            CancellationToken cancellationToken) =>
            StageSubmitForApprovalAsync(db, stockOutId, null, userId, cancellationToken);

        private async Task StageSubmitForApprovalAsync(
            AppDbContext db,
            int stockOutId,
            byte[]? expectedRowVersion,
            int userId,
            CancellationToken cancellationToken)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockOut);
            var stockOut = db.StockOuts
                .Include(item => item.Lines)
                    .ThenInclude(line => line.ProductSerials)
                .SingleOrDefault(item => item.Id == stockOutId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu xuất kho.");
            await StockDocumentDraftValidator.ValidateAsync(db, stockOut.Lines.ToArray(), cancellationToken);
            var beforeJson = Serialize(stockOut);
            if (expectedRowVersion is not null)
            {
                db.Entry(stockOut).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            }
            var lifecycle = new StockDocumentLifecycleService();
            stockOut.Status = lifecycle.SubmitForApproval(ParseStatus(stockOut.Status)).ToString();
            stockOut.UpdatedBy = userId;
            stockOut.UpdatedAt = DateTime.UtcNow;
            AddAudit(db, "SUBMIT", stockOut.Id, beforeJson, Serialize(stockOut), userId);
        }

        public Task ApproveAsync(
            int stockOutId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.approve", operationId),
                (db, token) => StageApproveAsync(db, stockOutId, userId),
                (db, token) => db.StockOuts.AnyAsync(
                    item => item.Id == stockOutId && item.Status == DocumentStatus.Approved, token),
                cancellationToken: cancellationToken);

        public async Task<byte[]> ApproveAsync(
            int stockOutId, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.approve", operationId),
                (db, token) => StageApproveAsync(db, stockOutId, rowVersion, userId),
                (db, token) => db.StockOuts.AnyAsync(
                    item => item.Id == stockOutId && item.Status == DocumentStatus.Approved, token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockOutId, cancellationToken);
        }

        internal virtual void Approve(int stockOutId, int userId) =>
            ApproveAsync(stockOutId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageApproveAsync(AppDbContext db, int stockOutId, int userId) =>
            StageApproveAsync(db, stockOutId, null, userId);

        private Task StageApproveAsync(
            AppDbContext db,
            int stockOutId,
            byte[]? expectedRowVersion,
            int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockOut);
            var stockOut = db.StockOuts.SingleOrDefault(item => item.Id == stockOutId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu xuất kho.");
            var beforeJson = Serialize(stockOut);
            if (expectedRowVersion is not null)
            {
                db.Entry(stockOut).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            }
            var lifecycle = new StockDocumentLifecycleService();
            stockOut.Status = lifecycle.Approve(
                ParseStatus(stockOut.Status),
                AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock)).ToString();
            stockOut.ApprovedBy = userId;
            stockOut.ApprovedAt = DateTime.UtcNow;
            stockOut.UpdatedBy = userId;
            stockOut.UpdatedAt = DateTime.UtcNow;
            AddAudit(db, "APPROVE", stockOut.Id, beforeJson, Serialize(stockOut), userId);
            return Task.CompletedTask;
        }

        public Task PostAsync(
            int stockOutId, int userId, Guid operationId,
            CancellationToken cancellationToken = default) =>
            _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.post", operationId),
                (db, token) => StagePostAsync(db, stockOutId, userId),
                (db, token) => db.StockOuts.AnyAsync(
                    item => item.Id == stockOutId && item.Status == DocumentStatus.Posted, token),
                cancellationToken: cancellationToken);

        public async Task<byte[]> PostAsync(
            int stockOutId, byte[] expectedRowVersion, int userId, Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-out.post", operationId),
                (db, token) => StagePostAsync(db, stockOutId, rowVersion, userId),
                (db, token) => db.StockOuts.AnyAsync(
                    item => item.Id == stockOutId && item.Status == DocumentStatus.Posted, token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockOutId, cancellationToken);
        }

        internal void Post(int stockOutId, int userId) =>
            PostAsync(stockOutId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StagePostAsync(AppDbContext db, int stockOutId, int userId) =>
            StagePostAsync(db, stockOutId, null, userId);

        private Task StagePostAsync(
            AppDbContext db,
            int stockOutId,
            byte[]? expectedRowVersion,
            int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockOut);

            var stockOut = db.StockOuts
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.Id == stockOutId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu xuất kho.");
            if (expectedRowVersion is not null)
            {
                db.Entry(stockOut).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            }
            var lifecycle = new StockDocumentLifecycleService();
            lifecycle.EnsureCanPost(ParseStatus(stockOut.Status));
            if (!AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock))
            {
                throw new InventoryDomainException("You are not authorized to approve stock documents.");
            }

            if (stockOut.Lines.Count == 0)
            {
                throw new InventoryDomainException("Phiếu xuất kho phải có ít nhất một dòng hàng.");
            }

            // tính lại hệ số từ database khi post để không dùng conversion factor draft đã cũ.
            var lineProductIds = stockOut.Lines.Select(l => l.ProductId).Distinct().ToList();
            var productMap = db.Products
                .Where(product => lineProductIds.Contains(product.Id))
                .ToDictionary(product => product.Id);
            var unitMap = db.ProductUnits
                .Where(pu => lineProductIds.Contains(pu.ProductId))
                .ToList();
            db.StockBalances
                .Where(balance =>
                    lineProductIds.Contains(balance.ProductId) &&
                    balance.WarehouseId == stockOut.WarehouseId)
                .Load();

            foreach (var line in stockOut.Lines)
            {
                var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
                line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
            }

            var beforeJson = Serialize(stockOut);

            // snapshot serial theo line được tạo trước khi gọi posting service.
            var lineSerialsMap = new Dictionary<int, List<string>>();
            var allDocumentSerials = new List<string>();
            var stockOutLineIds = stockOut.Lines.Select(line => line.Id).ToList();
            var legacySerialsByLineId = db.ProductSerials
                .Where(serial =>
                    serial.LastStockOutLineId.HasValue &&
                    stockOutLineIds.Contains(serial.LastStockOutLineId.Value))
                .Select(serial => new { LineId = serial.LastStockOutLineId!.Value, serial.SerialNumber })
                .ToList()
                .ToLookup(serial => serial.LineId, serial => serial.SerialNumber);
            foreach (var line in stockOut.Lines)
            {
                var serials = !string.IsNullOrWhiteSpace(line.DraftSerials)
                    ? line.DraftSerials
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(serial => serial.Trim())
                        .ToList()
                    : legacySerialsByLineId[line.Id].ToList();
                lineSerialsMap[line.Id] = serials;
                allDocumentSerials.AddRange(serials);
            }

            var normalizedAllDocumentSerials = QuanLyHangHoa.Helpers.SerialNumberNormalizer.NormalizeAll(allDocumentSerials);
            var dbSerialsByNumber = db.ProductSerials
                .Where(serial => normalizedAllDocumentSerials.Contains(serial.SerialNumber))
                .ToList()
                .ToDictionary(serial => serial.SerialNumber, StringComparer.OrdinalIgnoreCase);

            foreach (var line in stockOut.Lines)
            {
                var serials = lineSerialsMap[line.Id];
                if (serials.Count == 0 && !string.IsNullOrWhiteSpace(line.DraftSerials))
                {
                    serials = line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                }
                else if (serials.Count == 0)
                {
                    // dữ liệu cũ không có DraftSerials được truy ngược qua LastStockOutLineId.
                    serials = legacySerialsByLineId[line.Id].ToList();
                }

                productMap.TryGetValue(line.ProductId, out var product);
                if (product != null && product.IsSerialTracked)
                {
                    if (line.BaseQuantity != decimal.Truncate(line.BaseQuantity))
                    {
                        throw new InventoryDomainException(
                            $"Sản phẩm {product.DisplayName} theo dõi serial nên số lượng cơ sở phải là số nguyên.");
                    }

                    var requiredSerialCount = (int)line.BaseQuantity;
                    if (serials.Count != requiredSerialCount)
                    {
                        throw new InventoryDomainException(
                            $"Sản phẩm {product.DisplayName} yêu cầu {requiredSerialCount} serial, nhưng hiện có {serials.Count}.");
                    }

                    // mọi serial phải còn InStock, đúng sản phẩm và đúng kho nguồn trước khi trừ tồn.

                    var invalidSerials = new List<string>();
                    foreach (var sn in serials)
                    {
                        dbSerialsByNumber.TryGetValue(sn, out var dbSerial);
                        if (dbSerial == null || 
                            dbSerial.ProductId != line.ProductId || 
                            dbSerial.CurrentWarehouseId != stockOut.WarehouseId || 
                            dbSerial.CurrentStatus != "InStock")
                        {
                            invalidSerials.Add(sn);
                        }
                    }

                    if (invalidSerials.Any())
                    {
                        throw new Exception($"Các số serial sau đã được xuất kho ở phiếu khác hoặc không còn tồn kho trong kho này: [{string.Join(", ", invalidSerials)}]. Vui lòng sửa lại phiếu nháp trước khi duyệt.");
                    }
                }
            }

            // chặn một serial xuất hiện ở nhiều dòng của cùng chứng từ.
            var duplicateDocumentSerials = allDocumentSerials
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateDocumentSerials.Any())
            {
                throw new Exception($"Các số serial sau bị trùng lặp trong phiếu: [{string.Join(", ", duplicateDocumentSerials)}]. Vui lòng kiểm tra lại trước khi duyệt.");
            }

            // SaveChanges này vẫn nằm trong transaction; lỗi ở bất kỳ line posting nào sẽ rollback trạng thái PostedBy/PostedAt.
            stockOut.PostedBy = userId;
            stockOut.PostedAt = DateTime.UtcNow;

            var unitOfWork = new EfInventoryUnitOfWork(db, commitChanges: false);
            var documentSerialSet = allDocumentSerials.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var preloadedSerialEntities = db.ProductSerials.Local
                .Where(s => documentSerialSet.Contains(s.SerialNumber))
                .ToList();
            unitOfWork.MarkSerialsLoaded(preloadedSerialEntities);
            unitOfWork.MarkSerialsLoaded(allDocumentSerials);
            var postingService = new InventoryPostingService(
                unitOfWork,
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // thống nhất thứ tự ProductId giữa các chứng từ để giảm khả năng deadlock khi cập nhật balance.
            foreach (var line in stockOut.Lines.OrderBy(l => l.ProductId))
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns.ToArray() : Array.Empty<string>();
                
                postingService.PostStockOut(new PostStockOutCommand(
                    stockOut.Id,
                    stockOut.WarehouseId,
                    ParseKind(stockOut.PurposeCode),
                    StockDocumentStatus.Approved,
                    line.ProductId,
                    line.BaseQuantity > 0 ? line.BaseQuantity : line.Quantity,
                    serials,
                    userId));
            }

            // gắn line nguồn sau khi xuất để truy vết lần rời kho gần nhất của serial.
            foreach (var line in stockOut.Lines)
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns : new List<string>();
                if (serials.Any())
                {
                    var dbSerials = serials
                        .Where(dbSerialsByNumber.ContainsKey)
                        .Select(serialNumber => dbSerialsByNumber[serialNumber])
                        .ToList();
                    foreach (var s in dbSerials)
                    {
                        s.LastStockOutLineId = line.Id;
                    }
                }
            }

            var afterJson = Serialize(stockOut);
            AddAudit(db, "POST", stockOut.Id, beforeJson, afterJson, userId);
            return Task.CompletedTask;
        }

        // chuỗi tiếng Việt cũ được map về enum chuẩn để dữ liệu legacy vẫn dùng chung state machine.
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
                : throw new InventoryDomainException($"Unsupported stock-out status {status}.");
        }

        private static StockOutKind ParseKind(string purposeCode)
        {
            return purposeCode switch
            {
                "Sale" => StockOutKind.Sale,
                "Adjustment" => StockOutKind.Adjustment,
                "WarrantyReplacement" => StockOutKind.WarrantyReplacement,
                _ => throw new InventoryDomainException($"Unsupported stock-out purpose {purposeCode}.")
            };
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

        private async Task<byte[]> LoadRowVersionAsync(int id, CancellationToken cancellationToken)
        {
            await using var db = _contextFactory();
            return (await db.StockOuts.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken))?.ToArray()
                ?? throw new InventoryDomainException("Không tìm thấy phiếu xuất kho.");
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
                new DatabaseWriteRequest("stock-out.delete", operationId),
                (db, token) => StageDeleteAsync(db, id, rowVersion, userId),
                entityKey: id.ToString(),
                cancellationToken: cancellationToken);
        }
        private Task StageDeleteAsync(AppDbContext db, int id, byte[] expectedRowVersion, int userId)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockOut);
            var stockOut = db.StockOuts.Include(item => item.Lines).FirstOrDefault(item => item.Id == id)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu xuất kho.");
            if (stockOut.Status != DocumentStatus.Draft)
            {
                throw new InventoryDomainException("Chỉ có thể xóa phiếu xuất kho ở trạng thái nháp.");
            }

            db.Entry(stockOut).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;

            var beforeJson = JsonSerializer.Serialize(new { stockOut.Id, stockOut.DocumentCode });
            db.StockOutLines.RemoveRange(stockOut.Lines);
            db.StockOuts.Remove(stockOut);
            AddAudit(db, "DELETE", id, beforeJson, null, userId);
            return Task.CompletedTask;
        }

        private string Serialize(StockOut s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.DocumentCode,
                s.WarehouseId,
                s.CustomerId,
                s.ExportDate,
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
                    l.UnitPrice,
                    DraftSerials = string.IsNullOrEmpty(l.DraftSerials) ? null :
                                   (l.DraftSerials.Length > 150 ? l.DraftSerials.Substring(0, 150) + "... (truncated)" : l.DraftSerials)
                }).ToList()
            });
        }

        // audit POST tham gia transaction hiện tại; audit draft/delete tự commit cùng thao tác tương ứng.
        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockOut",
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
