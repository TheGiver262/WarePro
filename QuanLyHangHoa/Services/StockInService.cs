using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// quản lý draft và lifecycle phiếu nhập; khi post sẽ quy đổi về đơn vị gốc rồi ghi tồn, serial và audit.
    /// </summary>
    public class StockInService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public StockInService(Func<AppDbContext> contextFactory)
            : this(contextFactory, new DatabaseWriteExecutor(contextFactory))
        {
        }

        internal StockInService(
            Func<AppDbContext> contextFactory,
            DatabaseWriteExecutor writeExecutor)
        {
            _contextFactory = contextFactory;
            _writeExecutor = writeExecutor;
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

        public virtual List<StockIn> GetStockInPaged(
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status,
            int skip,
            int take)
        {
            using var db = _contextFactory();
            var query = db.StockIns.AsNoTracking()
                .Include(s => s.Supplier)
                .Include(s => s.Creator)
                .Include(s => s.Warehouse)
                .Include(s => s.Lines)
                    .ThenInclude(d => d.Product)
                .AsQueryable();

            query = ApplyStockInFilters(query, code, supplierName, startDate, endDate, warehouseId, status);

            return query
                .OrderByDescending(s => s.ImportDate)
                .ThenByDescending(s => s.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public virtual DocumentListStats GetStockInStats(
            string code,
            string supplierName,
            DateTime? startDate,
            DateTime? endDate,
            int? warehouseId,
            string status)
        {
            using var db = _contextFactory();
            var query = ApplyStockInFilters(
                db.StockIns.AsNoTracking(),
                code,
                supplierName,
                startDate,
                endDate,
                warehouseId,
                status);

            return query
                .GroupBy(_ => 1)
                .Select(group => new DocumentListStats
                {
                    TotalCount = group.Count(),
                    DraftCount = group.Count(stockIn =>
                        stockIn.Status == DocumentStatus.Draft || stockIn.Status == "nháp"),
                    PostedCount = group.Count(stockIn =>
                        stockIn.Status == DocumentStatus.Posted || stockIn.Status == "đã ghi sổ")
                })
                .SingleOrDefault() ?? new DocumentListStats();
        }

        private IQueryable<StockIn> ApplyStockInFilters(
            IQueryable<StockIn> query,
            string code,
            string supplierName,
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

            if (!string.IsNullOrWhiteSpace(supplierName))
            {
                var term = supplierName.Trim();
                query = query.Where(s => s.Supplier != null && s.Supplier.DisplayName.Contains(term));
            }

            if (startDate.HasValue)
            {
                query = query.Where(s => s.ImportDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                var endOfDay = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(s => s.ImportDate <= endOfDay);
            }

            if (warehouseId.HasValue && warehouseId.Value > 0)
            {
                query = query.Where(s => s.WarehouseId == warehouseId.Value);
            }

            if (!string.IsNullOrEmpty(status) && status != "Tất cả")
            {
                string targetStatus = status == "Đã ghi sổ" ? DocumentStatus.Posted : DocumentStatus.Draft;
                query = query.Where(s => s.Status == targetStatus || (targetStatus == DocumentStatus.Draft && s.Status == "nháp") || (targetStatus == DocumentStatus.Posted && s.Status == "đã ghi sổ"));
            }

            return query;
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

        public async Task SaveDraftAsync(
            StockIn stockIn,
            List<StockInLine> lines,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stockIn);
            ArgumentNullException.ThrowIfNull(lines);
            // chụp dữ liệu đầu vào thành giá trị thuần để mỗi lần retry dựng entity mới, không dùng lại object đang bị UI giữ và thay đổi.
            var snapshot = StockInDraftSnapshot.Create(stockIn, lines, userId);
            if (snapshot.Id > 0 && snapshot.RowVersion.Length == 0)
            {
                throw new ArgumentException("RowVersion is required for draft updates.", nameof(stockIn));
            }
            var savedId = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-in.save-draft", operationId),
                (db, token) => StageSaveDraftAsync(db, snapshot, token),
                entityKey: snapshot.DocumentCode,
                cancellationToken: cancellationToken);
            stockIn.Id = savedId;
            stockIn.DocumentCode = snapshot.DocumentCode;
            stockIn.RowVersion = await LoadRowVersionAsync(savedId, cancellationToken);
        }

        internal virtual void SaveDraft(StockIn stockIn, List<StockInLine> lines, int userId) =>
            SaveDraftAsync(stockIn, lines, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private async Task<int> StageSaveDraftAsync(
            AppDbContext db,
            StockInDraftSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            AuthorizationService.RequireFreshActor(db, snapshot.UserId, PermissionAction.PostStockIn);

            StockIn? existing = null;
            if (snapshot.Id > 0)
            {
                existing = db.StockIns
                    .Include(s => s.Lines)
                        .ThenInclude(l => l.ProductSerials)
                    .FirstOrDefault(s => s.Id == snapshot.Id);
            }

            if (snapshot.Id > 0 && existing is null)
            {
                throw new InventoryDomainException("Không tìm thấy chứng từ hoặc chứng từ đã bị xóa.");
            }

            // dựng line mới trong từng lần chạy để retry không mang theo tracking state từ dbcontext đã thất bại.
            var freshLines = snapshot.Lines.Select(line => line.ToEntity()).ToList();
            var productIds = freshLines.Select(line => line.ProductId).Distinct().ToList();
            var unitMap = db.ProductUnits
                .Where(productUnit => productIds.Contains(productUnit.ProductId))
                .ToList();
            foreach (var line in freshLines)
            {
                var productUnit = unitMap.FirstOrDefault(item =>
                    item.ProductId == line.ProductId && item.UnitId == line.UnitId);
                line.BaseQuantity = line.Quantity * (productUnit?.ConversionFactor ?? 1m);
            }

            if (existing is not null)
            {
                var lifecycle = new StockDocumentLifecycleService();
                lifecycle.EnsureCanEditDetails(ParseStatus(existing.Status));
                var beforeJson = Serialize(existing);
                // gắn rowversion người dùng đã đọc làm giá trị gốc; ef sẽ báo conflict nếu phiếu đã bị máy khác sửa.
                db.Entry(existing).Property(item => item.RowVersion).OriginalValue = snapshot.RowVersion;

                existing.WarehouseId = snapshot.WarehouseId;
                existing.SupplierId = snapshot.SupplierId;
                existing.ImportDate = snapshot.ImportDate;
                existing.Notes = snapshot.Notes;
                existing.UpdatedAt = snapshot.Timestamp;
                existing.UpdatedBy = snapshot.UserId;

                var oldSerials = existing.Lines.SelectMany(line => line.ProductSerials).ToList();
                if (oldSerials.Count > 0)
                {
                    db.ProductSerials.RemoveRange(oldSerials);
                }

                db.StockInLines.RemoveRange(existing.Lines);
                existing.Lines = freshLines;
                AddAudit(db, "UPDATE", existing.Id, beforeJson, Serialize(existing), snapshot.UserId);
                return existing.Id;
            }

            var document = new StockIn
            {
                DocumentCode = snapshot.DocumentCode,
                SupplierId = snapshot.SupplierId,
                WarehouseId = snapshot.WarehouseId == 0
                    ? new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId()
                    : snapshot.WarehouseId,
                PurposeCode = snapshot.PurposeCode,
                ImportDate = snapshot.ImportDate,
                Notes = snapshot.Notes,
                Status = DocumentStatus.Draft,
                CreatedBy = snapshot.UserId,
                CreatedAt = snapshot.Timestamp,
                Lines = freshLines
            };
            db.StockIns.Add(document);
            // flush sớm để database cấp id cho phiếu và các line; audit phía sau cần khóa thật để truy vết đúng.
            await db.SaveChangesAsync(cancellationToken);
            AddAudit(db, "CREATE", document.Id, null, Serialize(document), snapshot.UserId);
            return document.Id;
        }
        internal Task SubmitForApprovalAsync(
            int stockInId,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default) =>
            SubmitForApprovalAsync(
                stockInId,
                GetCurrentRowVersion(stockInId),
                userId,
                operationId,
                cancellationToken);

        public async Task<byte[]> SubmitForApprovalAsync(
            int stockInId,
            byte[] expectedRowVersion,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-in.submit", operationId),
                (db, token) => StageSubmitForApprovalAsync(db, stockInId, rowVersion, userId),
                (db, token) => db.StockIns.AnyAsync(
                    item => item.Id == stockInId && item.Status == DocumentStatus.PendingApproval,
                    token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockInId, cancellationToken);
        }

        internal virtual void SubmitForApproval(int stockInId, int userId) =>
            SubmitForApprovalAsync(stockInId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageSubmitForApprovalAsync(
            AppDbContext db,
            int stockInId,
            byte[] expectedRowVersion,
            int userId)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockIn);
            var stockIn = db.StockIns.SingleOrDefault(item => item.Id == stockInId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu nhập kho.");
            var beforeJson = Serialize(stockIn);
            db.Entry(stockIn).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            var lifecycle = new StockDocumentLifecycleService();
            stockIn.Status = lifecycle.SubmitForApproval(ParseStatus(stockIn.Status)).ToString();
            stockIn.UpdatedBy = userId;
            stockIn.UpdatedAt = DateTime.UtcNow;
            AddAudit(db, "SUBMIT", stockIn.Id, beforeJson, Serialize(stockIn), userId);
            return Task.CompletedTask;
        }

        internal Task ApproveAsync(
            int stockInId,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default) =>
            ApproveAsync(
                stockInId,
                GetCurrentRowVersion(stockInId),
                userId,
                operationId,
                cancellationToken);

        public async Task<byte[]> ApproveAsync(
            int stockInId,
            byte[] expectedRowVersion,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-in.approve", operationId),
                (db, token) => StageApproveAsync(db, stockInId, rowVersion, userId),
                (db, token) => db.StockIns.AnyAsync(
                    item => item.Id == stockInId && item.Status == DocumentStatus.Approved,
                    token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockInId, cancellationToken);
        }

        internal virtual void Approve(int stockInId, int userId) =>
            ApproveAsync(stockInId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StageApproveAsync(
            AppDbContext db,
            int stockInId,
            byte[] expectedRowVersion,
            int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockIn);
            var stockIn = db.StockIns.SingleOrDefault(item => item.Id == stockInId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu nhập kho.");
            var beforeJson = Serialize(stockIn);
            db.Entry(stockIn).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            var lifecycle = new StockDocumentLifecycleService();
            stockIn.Status = lifecycle.Approve(
                ParseStatus(stockIn.Status),
                AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock)).ToString();
            stockIn.ApprovedBy = userId;
            stockIn.ApprovedAt = DateTime.UtcNow;
            stockIn.UpdatedBy = userId;
            stockIn.UpdatedAt = DateTime.UtcNow;
            AddAudit(db, "APPROVE", stockIn.Id, beforeJson, Serialize(stockIn), userId);
            return Task.CompletedTask;
        }

        internal Task PostAsync(
            int stockInId,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default) =>
            PostAsync(
                stockInId,
                GetCurrentRowVersion(stockInId),
                userId,
                operationId,
                cancellationToken);

        public async Task<byte[]> PostAsync(
            int stockInId,
            byte[] expectedRowVersion,
            int userId,
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            var rowVersion = SnapshotRowVersion(expectedRowVersion);
            await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest("stock-in.post", operationId),
                (db, token) => StagePostAsync(db, stockInId, rowVersion, userId),
                (db, token) => db.StockIns.AnyAsync(
                    item => item.Id == stockInId && item.Status == DocumentStatus.Posted,
                    token),
                cancellationToken: cancellationToken);
            return await LoadRowVersionAsync(stockInId, cancellationToken);
        }

        internal virtual void Post(int stockInId, int userId) =>
            PostAsync(stockInId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

        private Task StagePostAsync(
            AppDbContext db,
            int stockInId,
            byte[] expectedRowVersion,
            int userId)
        {
            var actor = AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockIn);

            var stockIn = db.StockIns
                .Include(item => item.Lines)
                    .ThenInclude(line => line.ProductSerials)
                .FirstOrDefault(item => item.Id == stockInId)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu nhập kho.");
            db.Entry(stockIn).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            var lifecycle = new StockDocumentLifecycleService();
            lifecycle.EnsureCanPost(ParseStatus(stockIn.Status));
            if (!AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock))
            {
                throw new InventoryDomainException("You are not authorized to approve stock documents.");
            }

            if (stockIn.Lines.Count == 0)
            {
                throw new InventoryDomainException("Phiếu nhập kho phải có ít nhất một dòng hàng.");
            }

            // tính lại BaseQuantity từ database lúc post, không tin giá trị draft có thể đã cũ.
            var lineProductIds = stockIn.Lines.Select(l => l.ProductId).Distinct().ToList();
            var productMap = db.Products
                .Where(product => lineProductIds.Contains(product.Id))
                .ToDictionary(product => product.Id);
            var unitMap = db.ProductUnits
                .Where(pu => lineProductIds.Contains(pu.ProductId))
                .ToList();
            var balances = db.StockBalances
                .Where(balance =>
                    lineProductIds.Contains(balance.ProductId) &&
                    balance.WarehouseId == stockIn.WarehouseId)
                .ToList();
            var balanceProductIds = balances
                .Select(balance => balance.ProductId)
                .ToHashSet();
            foreach (var productId in productMap.Keys.Where(productId => !balanceProductIds.Contains(productId)))
            {
                db.StockBalances.Add(new StockBalance
                {
                    ProductId = productId,
                    WarehouseId = stockIn.WarehouseId
                });
            }

            foreach (var line in stockIn.Lines)
            {
                var pu = unitMap.FirstOrDefault(u => u.ProductId == line.ProductId && u.UnitId == line.UnitId);
                line.BaseQuantity = line.Quantity * (pu?.ConversionFactor ?? 1m);
            }

            var beforeJson = Serialize(stockIn);

            // map theo line id giữ đúng serial của từng dòng sau khi toàn bộ validation hoàn tất.
            var lineSerialsMap = new Dictionary<int, List<string>>();
            var allDocumentSerials = new List<string>();
            foreach (var line in stockIn.Lines)
            {
                var serials = !string.IsNullOrWhiteSpace(line.DraftSerials)
                    ? line.DraftSerials
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(serial => serial.Trim())
                        .ToList()
                    : line.ProductSerials
                        .Select(serial => serial.SerialNumber)
                        .ToList();
                lineSerialsMap[line.Id] = serials;
                allDocumentSerials.AddRange(serials);
            }

            var existingDbSerialNumbers = db.ProductSerials
                .Where(serial => allDocumentSerials.Contains(serial.SerialNumber))
                .Select(serial => serial.SerialNumber)
                .ToHashSet(StringComparer.Ordinal);


            foreach (var line in stockIn.Lines)
            {
                var serials = lineSerialsMap[line.Id];
                if (serials.Count == 0 && !string.IsNullOrWhiteSpace(line.DraftSerials))
                {
                    serials = line.DraftSerials.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                }
                else if (serials.Count == 0)
                {
                    // dữ liệu cũ chưa có DraftSerials dùng navigation ProductSerial làm nguồn tương thích.
                    serials = line.ProductSerials.Select(ps => ps.SerialNumber).ToList();
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

                    // serial nhập mới không được trùng bất kỳ serial đã tồn tại trong hệ thống.
                    var existingDbSerials = serials
                        .Where(existingDbSerialNumbers.Contains)
                        .ToList();

                    if (existingDbSerials.Any())
                    {
                        throw new InventoryDomainException($"Số serial [{string.Join(", ", existingDbSerials)}] đã tồn tại trong hệ thống. Vui lòng kiểm tra và chỉnh sửa lại phiếu nháp trước khi duyệt.");
                    }
                }
            }

            // cùng một serial không được xuất hiện ở hai dòng của cùng chứng từ, kể cả khác hoa thường.
            var duplicateDocumentSerials = allDocumentSerials
                .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateDocumentSerials.Any())
            {
                throw new InventoryDomainException($"Các số serial sau bị trùng lặp trong phiếu: [{string.Join(", ", duplicateDocumentSerials)}]. Vui lòng kiểm tra lại trước khi duyệt.");
            }

            // trạng thái trung gian đã được SaveChanges nhưng vẫn nằm trong transaction và sẽ rollback nếu posting lỗi.
            stockIn.PostedBy = userId;
            stockIn.PostedAt = DateTime.UtcNow;

            var unitOfWork = new EfInventoryUnitOfWork(db, commitChanges: false);
            unitOfWork.MarkSerialsLoaded(allDocumentSerials);
            var postingService = new InventoryPostingService(
                unitOfWork,
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            // mọi chứng từ khóa sản phẩm theo cùng thứ tự ProductId để giảm vòng chờ deadlock.
            foreach (var line in stockIn.Lines.OrderBy(l => l.ProductId))
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns.ToArray() : Array.Empty<string>();

                postingService.PostStockIn(new PostStockInCommand(
                    stockIn.Id,
                    stockIn.WarehouseId,
                    ParseKind(stockIn.PurposeCode),
                    StockDocumentStatus.Approved,
                    line.ProductId,
                    line.BaseQuantity > 0 ? line.BaseQuantity : line.Quantity,
                    serials,
                    userId,
                    StockInLineId: line.Id));
            }

            // sau khi serial được tạo, gắn lại line nguồn để truy xuất lịch sử nhập của từng serial.
            foreach (var line in stockIn.Lines)
            {
                var serials = lineSerialsMap.TryGetValue(line.Id, out var sns) ? sns : new List<string>();
                if (serials.Any())
                {
                    var dbSerials = db.ProductSerials.Local
                        .Where(ps => serials.Contains(ps.SerialNumber))
                        .ToList();
                    foreach (var s in dbSerials)
                    {
                        s.LastStockInLineId = line.Id;
                    }
                }
            }

            var afterJson = Serialize(stockIn);
            AddAudit(db, "POST", stockIn.Id, beforeJson, afterJson, userId);
            return Task.CompletedTask;
        }

        // chấp nhận cả chuỗi chuẩn và giá trị tiếng Việt cũ để chứng từ legacy vẫn đi đúng lifecycle.
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
                : throw new InventoryDomainException($"Unsupported stock-in status {status}.");
        }

        private static StockInKind ParseKind(string purposeCode)
        {
            return purposeCode switch
            {
                "Purchase" => StockInKind.Purchase,
                "OpeningBalance" => StockInKind.OpeningBalance,
                "Adjustment" => StockInKind.Adjustment,
                "WarrantyReceive" => StockInKind.WarrantyReceive,
                _ => throw new InventoryDomainException($"Unsupported stock-in purpose {purposeCode}.")
            };
        }

        // dải giữ nguyên prefix và độ dài phần số, ví dụ SP001-SP003 thành ba serial riêng.
        public static List<string> ParseSerialRange(string input)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(input)) return result;

            var parts = input.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var rangeMatch = Regex.Match(trimmed, @"^(.+?)(\d+)-[^\d]*(\d+)$");
                string startStr = rangeMatch.Groups[2].Value;
                string endStr = rangeMatch.Groups[3].Value;
                if (rangeMatch.Success &&
                    long.TryParse(startStr, out long start) &&
                    long.TryParse(endStr, out long end) &&
                    end >= start)
                {
                    string prefix = rangeMatch.Groups[1].Value;
                    int padLen = startStr.Length;
                    for (long i = start; i <= end; i++)
                        result.Add(prefix + i.ToString().PadLeft(padLen, '0'));
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

        private byte[] GetCurrentRowVersion(int id)
        {
            using var db = _contextFactory();
            return db.StockIns.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.RowVersion)
                .SingleOrDefault()?.ToArray()
                ?? throw new InventoryDomainException("Không tìm thấy phiếu nhập kho.");
        }

        private async Task<byte[]> LoadRowVersionAsync(int id, CancellationToken cancellationToken)
        {
            await using var db = _contextFactory();
            return (await db.StockIns.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken))?.ToArray()
                ?? throw new InventoryDomainException("Không tìm thấy phiếu nhập kho.");
        }

        private static byte[] SnapshotRowVersion(byte[] expectedRowVersion)
        {
            ArgumentNullException.ThrowIfNull(expectedRowVersion);
            if (expectedRowVersion.Length == 0)
                throw new ArgumentException("RowVersion is required.", nameof(expectedRowVersion));
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
                new DatabaseWriteRequest("stock-in.delete", operationId),
                (db, token) => StageDeleteAsync(db, id, rowVersion, userId),
                cancellationToken: cancellationToken);
        }

        private Task StageDeleteAsync(
            AppDbContext db,
            int id,
            byte[] expectedRowVersion,
            int userId)
        {
            AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockIn);
            var stockIn = db.StockIns
                .Include(item => item.Lines)
                .FirstOrDefault(item => item.Id == id)
                ?? throw new InventoryDomainException("Không tìm thấy phiếu nhập kho.");
            if (stockIn.Status != DocumentStatus.Draft)
            {
                throw new InventoryDomainException("Chỉ có thể xóa phiếu nhập kho ở trạng thái nháp.");
            }

            db.Entry(stockIn).Property(item => item.RowVersion).OriginalValue = expectedRowVersion;
            var beforeJson = JsonSerializer.Serialize(new { stockIn.Id, stockIn.DocumentCode });
            db.StockInLines.RemoveRange(stockIn.Lines);
            db.StockIns.Remove(stockIn);
            AddAudit(db, "DELETE", id, beforeJson, null, userId);
            return Task.CompletedTask;
        }

        private sealed record StockInDraftSnapshot(
            int Id,
            string DocumentCode,
            int? SupplierId,
            int WarehouseId,
            string PurposeCode,
            DateTime? ImportDate,
            string? Notes,
            byte[] RowVersion,
            int UserId,
            DateTime Timestamp,
            StockInLineSnapshot[] Lines)
        {
            public static StockInDraftSnapshot Create(
                StockIn source,
                IEnumerable<StockInLine> lines,
                int userId)
            {
                var timestamp = DateTime.Now;
                var documentCode = string.IsNullOrWhiteSpace(source.DocumentCode)
                    ? $"SI-{timestamp:yyyyMMddHHmmss}"
                    : source.DocumentCode;
                return new StockInDraftSnapshot(
                    source.Id,
                    documentCode,
                    source.SupplierId,
                    source.WarehouseId,
                    source.PurposeCode,
                    source.ImportDate,
                    source.Notes,
                    source.RowVersion.ToArray(),
                    userId,
                    timestamp,
                    lines.Select(StockInLineSnapshot.Create).ToArray());
            }
        }

        private sealed record StockInLineSnapshot(
            int ProductId,
            int UnitId,
            decimal Quantity,
            decimal UnitPrice,
            string? DraftSerials)
        {
            public static StockInLineSnapshot Create(StockInLine source)
            {
                var serials = (source.ProductSerials ?? [])
                    .Select(serial => serial.SerialNumber.Trim())
                    .Where(serial => serial.Length > 0)
                    .ToArray();
                return new StockInLineSnapshot(
                    source.ProductId,
                    source.UnitId,
                    source.Quantity,
                    source.UnitPrice,
                    serials.Length == 0 ? null : string.Join(",", serials));
            }

            public StockInLine ToEntity() => new()
            {
                ProductId = ProductId,
                UnitId = UnitId,
                Quantity = Quantity,
                UnitPrice = UnitPrice,
                DraftSerials = DraftSerials
            };
        }
        private string Serialize(StockIn s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.DocumentCode,
                s.WarehouseId,
                s.SupplierId,
                s.ImportDate,
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

        // SaveChanges tham gia ambient transaction khi được gọi từ Post; các thao tác draft tự commit audit riêng.
        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockIn",
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
