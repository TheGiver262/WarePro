using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

/// <summary>
/// đảo phiếu đã post bằng adjustment và ledger ngược để giữ nguyên lịch sử nguồn.
/// </summary>
public sealed class StockReversalService
{
    private const string ReversalType = "Reversal";
    private readonly Func<AppDbContext> _contextFactory;
    private readonly DatabaseWriteExecutor _writeExecutor;

    public StockReversalService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _writeExecutor = new DatabaseWriteExecutor(_contextFactory);
    }

    public Task<int> ReverseDocumentAsync(
        string sourceType, int sourceId, int userId, Guid operationId,
        CancellationToken cancellationToken = default) =>
        ReverseDocumentAsync(
            sourceType,
            sourceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "SYSTEM-REVERSAL",
            userId,
            operationId,
            cancellationToken);

    public Task<int> ReverseDocumentAsync(
        string sourceType, string sourceReference, string reason, int userId, Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedSourceType = NormalizeSourceType(sourceType);
        var normalizedSourceReference = NormalizeSourceReference(sourceReference);
        var normalizedReason = NormalizeReason(reason);
        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest("stock-reversal.reverse", operationId),
            (db, token) => StageReverseDocumentAsync(
                db, normalizedSourceType, normalizedSourceReference, normalizedReason, userId, token),
            (db, token) => HasCommittedReversalAsync(
                db, normalizedSourceType, normalizedSourceReference, token),
            entityKey: $"{normalizedSourceType}:{normalizedSourceReference}",
            cancellationToken: cancellationToken);
    }

    internal int ReverseDocument(string sourceType, int sourceId, int userId) =>
        ReverseDocumentAsync(sourceType, sourceId, userId, Guid.NewGuid()).GetAwaiter().GetResult();

    private static async Task<int> StageReverseDocumentAsync(
        AppDbContext db,
        string normalizedSourceType,
        string normalizedSourceReference,
        string reason,
        int userId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            throw new InventoryDomainException("Người thực hiện đảo chứng từ không hợp lệ.");
        }

        AuthorizationService.RequireFreshActor(db, userId, PermissionAction.PostStockAdjustment);
        var sourceId = await ResolveSourceIdAsync(
            db, normalizedSourceType, normalizedSourceReference, cancellationToken);

        // kiểm tra trước kết hợp unique filtered index trong database để chặn reversal lặp do cạnh tranh.
            if (db.StockAdjustments.Any(adjustment =>
                    adjustment.AdjustmentType == ReversalType &&
                    adjustment.ReferenceDocumentType == normalizedSourceType &&
                    adjustment.ReferenceDocumentId == sourceId))
            {
                throw new InventoryDomainException("Chứng từ kho này đã được đảo trước đó.");
            }

            // ledger nguồn là cơ sở tính lượng đảo; không dựng lại từ line có thể đã mang dữ liệu hiển thị cũ.
            var entries = db.StockLedgers
                .Where(entry =>
                    entry.SourceDocumentType == normalizedSourceType &&
                    entry.SourceDocumentId == sourceId)
                .OrderBy(entry => entry.Id)
                .ToList();

            if (entries.Count == 0)
            {
                throw new InventoryDomainException("Không tìm thấy chứng từ kho đã ghi sổ.");
            }

            // toàn bộ điều kiện nguồn được xác minh trước khi thay đổi balance hoặc serial đầu tiên.
            var warehouseId = GetSingleWarehouse(entries);
            EnsureSourceIsPosted(db, normalizedSourceType, sourceId, warehouseId);
            ValidateLedgerDirections(entries, normalizedSourceType);
            ApplyInverseBalances(db, entries, normalizedSourceType);
            RestoreSerials(db, normalizedSourceType, sourceId, warehouseId);
            EnsureNoActiveBusinessLinks(db, normalizedSourceType, sourceId);

            var now = DateTime.UtcNow;
            var reversal = new StockAdjustment
            {
                DocumentCode = $"REV-{normalizedSourceType}-{sourceId}",
                WarehouseId = warehouseId,
                AdjustmentType = ReversalType,
                ReasonCode = reason == "SYSTEM-REVERSAL" ? "SYSTEM-REVERSAL" : "USER-REVERSAL",
                Status = "Posted",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = now,
                ReferenceDocumentType = normalizedSourceType,
                ReferenceDocumentId = sourceId,
                Notes = reason
            };

            db.StockAdjustments.Add(reversal);
            // cần id adjustment trước khi sinh ledger ngược; flush này vẫn nằm trong transaction đảo phiếu.
            await db.SaveChangesAsync(cancellationToken);

            // mỗi entry nguồn sinh một entry ngược chiều, cùng quantity và vị trí để audit có thể đối chiếu 1-1.
            foreach (var entry in entries)
            {
                db.StockLedgers.Add(new StockLedger
                {
                    WarehouseId = entry.WarehouseId,
                    ProductId = entry.ProductId,
                    ProductSerialId = entry.ProductSerialId,
                    SourceDocumentType = "StockAdjustment",
                    SourceDocumentId = reversal.Id,
                    MovementType = entry.MovementType == "In" ? "Out" : "In",
                    Quantity = entry.Quantity,
                    PostedBy = userId,
                    PostedAt = now
                });
            }

            MarkSourceReversed(db, normalizedSourceType, sourceId, userId, now);
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = normalizedSourceType,
                EntityId = sourceId,
                ActionCode = "Reverse",
                BeforeJson = "{\"Status\":\"Posted\"}",
                AfterJson = $"{{\"Status\":\"Reversed\",\"AdjustmentId\":{reversal.Id}}}",
                PerformedBy = userId,
                PerformedAt = now
            });

            return reversal.Id;
    }

    internal int ReversePostedLedgerDocument(string sourceType, int sourceId, int userId) =>
        ReverseDocument(sourceType, sourceId, userId);

    private static string NormalizeSourceType(string sourceType)
    {
        if (string.Equals(sourceType?.Trim(), "StockIn", StringComparison.OrdinalIgnoreCase))
        {
            return "StockIn";
        }

        if (string.Equals(sourceType?.Trim(), "StockOut", StringComparison.OrdinalIgnoreCase))
        {
            return "StockOut";
        }

        throw new InventoryDomainException("Chỉ hỗ trợ đảo phiếu nhập kho hoặc phiếu xuất kho.");
    }

    private static string NormalizeSourceReference(string sourceReference)
    {
        var normalized = sourceReference?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InventoryDomainException("Mã chứng từ kho không hợp lệ.");
        return normalized;
    }

    private static string NormalizeReason(string reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InventoryDomainException("Vui lòng nhập lý do đảo chứng từ.");
        if (normalized.Length > 500)
            throw new InventoryDomainException("Lý do đảo chứng từ không được vượt quá 500 ký tự.");
        return normalized;
    }

    private static async Task<int> ResolveSourceIdAsync(
        AppDbContext db,
        string sourceType,
        string sourceReference,
        CancellationToken cancellationToken)
    {
        if (int.TryParse(sourceReference, out var sourceId) && sourceId > 0)
            return sourceId;

        sourceId = sourceType == "StockIn"
            ? await db.StockIns.Where(item => item.DocumentCode == sourceReference)
                .Select(item => item.Id).SingleOrDefaultAsync(cancellationToken)
            : await db.StockOuts.Where(item => item.DocumentCode == sourceReference)
                .Select(item => item.Id).SingleOrDefaultAsync(cancellationToken);

        if (sourceId <= 0)
            throw new InventoryDomainException("Không tìm thấy chứng từ kho cần đảo.");
        return sourceId;
    }

    private static async Task<bool> HasCommittedReversalAsync(
        AppDbContext db,
        string sourceType,
        string sourceReference,
        CancellationToken cancellationToken)
    {
        var sourceId = await ResolveSourceIdAsync(db, sourceType, sourceReference, cancellationToken);
        return await db.StockAdjustments.AnyAsync(adjustment =>
            adjustment.AdjustmentType == ReversalType &&
            adjustment.ReferenceDocumentType == sourceType &&
            adjustment.ReferenceDocumentId == sourceId,
            cancellationToken);
    }
    private static int GetSingleWarehouse(IReadOnlyCollection<StockLedger> entries)
    {
        var warehouseIds = entries.Select(entry => entry.WarehouseId).Distinct().ToArray();
        if (warehouseIds.Length != 1)
        {
            throw new InventoryDomainException("Chứng từ có dữ liệu kho không nhất quán, không thể đảo tự động.");
        }

        return warehouseIds[0];
    }

    private static void EnsureSourceIsPosted(
        AppDbContext db,
        string sourceType,
        int sourceId,
        int ledgerWarehouseId)
    {
        string? status;
        int sourceWarehouseId;

        if (sourceType == "StockIn")
        {
            var source = db.StockIns.SingleOrDefault(item => item.Id == sourceId);
            if (source is null)
            {
                throw new InventoryDomainException("Không tìm thấy phiếu nhập kho nguồn.");
            }

            status = source.Status;
            sourceWarehouseId = source.WarehouseId;
        }
        else
        {
            var source = db.StockOuts.SingleOrDefault(item => item.Id == sourceId);
            if (source is null)
            {
                throw new InventoryDomainException("Không tìm thấy phiếu xuất kho nguồn.");
            }

            status = source.Status;
            sourceWarehouseId = source.WarehouseId;
        }

        if (string.Equals(status, "Reversed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InventoryDomainException("Chứng từ kho này đã được đảo trước đó.");
        }

        if (!string.Equals(status, "Posted", StringComparison.OrdinalIgnoreCase))
        {
            throw new InventoryDomainException("Chỉ có thể đảo chứng từ kho đã ghi sổ.");
        }

        if (sourceWarehouseId != ledgerWarehouseId)
        {
            throw new InventoryDomainException("Kho trên chứng từ và sổ kho không nhất quán.");
        }
    }

    private static void EnsureNoActiveBusinessLinks(
        AppDbContext db,
        string sourceType,
        int sourceId)
    {
        if (sourceType == "StockIn")
        {
            if (db.PurchaseInvoices.Any(invoice =>
                    invoice.StockInId == sourceId &&
                    invoice.Status == InvoiceStatus.Active))
            {
                throw new InventoryDomainException(
                    "Cannot reverse stock-in while its purchase invoice is active.");
            }

            return;
        }

        if (db.SalesInvoices.Any(invoice =>
                invoice.StockOutId == sourceId &&
                invoice.Status == InvoiceStatus.Active))
        {
            throw new InventoryDomainException(
                "Cannot reverse stock-out while its sales invoice is active.");
        }

        if (db.WarrantyCoverages.Any(coverage =>
                coverage.CoverageStatus == "Active" &&
                coverage.SalesInvoice != null &&
                coverage.SalesInvoice.StockOutId == sourceId))
        {
            throw new InventoryDomainException(
                "Cannot reverse stock-out while linked warranty coverage is active.");
        }
    }


    private static void ValidateLedgerDirections(IEnumerable<StockLedger> entries, string sourceType)
    {
        var expectedDirection = sourceType == "StockIn" ? "In" : "Out";
        if (entries.Any(entry => !string.Equals(
                entry.MovementType,
                expectedDirection,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new InventoryDomainException("Chiều biến động trên sổ kho không khớp loại chứng từ.");
        }
    }

    // gom ledger theo sản phẩm-kho trước khi cập nhật; đảo StockIn phải chống âm vì hàng có thể đã phát sinh sau đó.
    private static void ApplyInverseBalances(
        AppDbContext db,
        IEnumerable<StockLedger> entries,
        string sourceType)
    {
        // quantity ledger luôn dương; source type quyết định phép cộng hoặc trừ bù.
        var movements = entries
            .GroupBy(entry => new { entry.ProductId, entry.WarehouseId })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.WarehouseId,
                Quantity = group.Sum(entry => entry.Quantity)
            })
            .ToList();
        var productIds = movements.Select(movement => movement.ProductId).Distinct().ToList();
        var warehouseIds = movements.Select(movement => movement.WarehouseId).Distinct().ToList();
        var balances = db.StockBalances
            .Where(balance =>
                productIds.Contains(balance.ProductId) &&
                warehouseIds.Contains(balance.WarehouseId))
            .ToDictionary(balance => (balance.ProductId, balance.WarehouseId));

        foreach (var movement in movements)
        {
            balances.TryGetValue((movement.ProductId, movement.WarehouseId), out var balance);

            if (sourceType == "StockIn")
            {
                if (balance is null ||
                    balance.OnHandQuantity < movement.Quantity ||
                    balance.AvailableQuantity < movement.Quantity)
                {
                    throw new InventoryDomainException(
                        "Không thể đảo phiếu nhập vì hàng đã được xuất, giữ chỗ hoặc điều chuyển.");
                }

                balance.OnHandQuantity -= movement.Quantity;
                balance.AvailableQuantity -= movement.Quantity;
                continue;
            }

            if (balance is null)
            {
                balance = new StockBalance
                {
                    ProductId = movement.ProductId,
                    WarehouseId = movement.WarehouseId
                };
                db.StockBalances.Add(balance);
            }

            balance.OnHandQuantity += movement.Quantity;
            balance.AvailableQuantity += movement.Quantity;
        }
    }

    // chỉ đảo serial chưa có nghiệp vụ tiếp theo; nếu lịch sử đã tiến triển thì từ chối toàn bộ reversal.
    private static void RestoreSerials(
        AppDbContext db,
        string sourceType,
        int sourceId,
        int warehouseId)
    {
        if (sourceType == "StockIn")
        {
            var lines = db.StockInLines
                .Where(line => line.StockInId == sourceId && line.Product.IsSerialTracked)
                .Select(line => new { line.Id, line.ProductId, line.BaseQuantity })
                .ToList();
            var lineIds = lines.Select(line => (int?)line.Id).ToArray();
            var serials = db.ProductSerials
                .Where(serial => lineIds.Contains(serial.LastStockInLineId))
                .ToList();

            EnsureSerialCountsMatch(lines.Select(line => (line.ProductId, line.BaseQuantity)), serials);
            foreach (var serial in serials)
            {
                if (serial.CurrentStatus != "InStock" ||
                    serial.CurrentWarehouseId != warehouseId ||
                    serial.LastStockOutLineId.HasValue ||
                    serial.StockTransferLineId.HasValue)
                {
                    throw new InventoryDomainException(
                        $"Serial {serial.SerialNumber} đã phát sinh giao dịch sau phiếu nhập.");
                }

                serial.CurrentStatus = "Inactive";
                serial.CurrentWarehouseId = null;
            }

            return;
        }

        var stockOutLines = db.StockOutLines
            .Where(line => line.StockOutId == sourceId && line.Product.IsSerialTracked)
            .Select(line => new { line.Id, line.ProductId, line.BaseQuantity })
            .ToList();
        var stockOutLineIds = stockOutLines.Select(line => line.Id).ToArray();
        var stockOutSerials = db.ProductSerials
            .Where(serial =>
                serial.LastStockOutLineId.HasValue &&
                stockOutLineIds.Contains(serial.LastStockOutLineId.Value))
            .ToList();

        EnsureSerialCountsMatch(
            stockOutLines.Select(line => (line.ProductId, line.BaseQuantity)),
            stockOutSerials);
        foreach (var serial in stockOutSerials)
        {
            if (serial.CurrentStatus != "Sold" || serial.CurrentWarehouseId.HasValue)
            {
                throw new InventoryDomainException(
                    $"Serial {serial.SerialNumber} đã phát sinh nghiệp vụ sau phiếu xuất.");
            }

            serial.CurrentStatus = "InStock";
            serial.CurrentWarehouseId = warehouseId;
            serial.LastStockOutLineId = null;
        }
    }

    // số serial thực tế phải khớp tổng BaseQuantity nguyên của từng sản phẩm trước khi đổi trạng thái.
    private static void EnsureSerialCountsMatch(
        IEnumerable<(int ProductId, decimal Quantity)> expectedLines,
        IReadOnlyCollection<ProductSerial> serials)
    {
        foreach (var expected in expectedLines
                     .GroupBy(line => line.ProductId)
                     .Select(group => new
                     {
                         ProductId = group.Key,
                         Quantity = group.Sum(line => line.Quantity)
                     }))
        {
            var actualCount = serials.Count(serial => serial.ProductId == expected.ProductId);
            if (expected.Quantity != decimal.Truncate(expected.Quantity) ||
                actualCount != (int)expected.Quantity)
            {
                throw new InventoryDomainException(
                    $"Số serial của sản phẩm {expected.ProductId} không khớp số lượng chứng từ.");
            }
        }
    }

    // source được giữ nguyên dữ liệu và chỉ đổi trạng thái để mọi tham chiếu lịch sử vẫn còn hiệu lực.
    private static void MarkSourceReversed(
        AppDbContext db,
        string sourceType,
        int sourceId,
        int userId,
        DateTime now)
    {
        if (sourceType == "StockIn")
        {
            var source = db.StockIns.Single(item => item.Id == sourceId);
            source.Status = "Reversed";
            source.UpdatedBy = userId;
            source.UpdatedAt = now;
            return;
        }

        var stockOut = db.StockOuts.Single(item => item.Id == sourceId);
        stockOut.Status = "Reversed";
        stockOut.UpdatedBy = userId;
        stockOut.UpdatedAt = now;
    }
}
