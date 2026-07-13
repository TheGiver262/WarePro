using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public sealed class StockReversalService
{
    private const string ReversalType = "Reversal";
    private readonly Func<AppDbContext> _contextFactory;

    public StockReversalService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public int ReverseDocument(string sourceType, int sourceId, int userId)
    {
        var normalizedSourceType = NormalizeSourceType(sourceType);
        if (sourceId <= 0)
        {
            throw new InventoryDomainException("Mã chứng từ kho không hợp lệ.");
        }

        if (userId <= 0)
        {
            throw new InventoryDomainException("Người thực hiện đảo chứng từ không hợp lệ.");
        }

        using var db = _contextFactory();
        using var transaction = db.Database.BeginTransaction();

        try
        {
            if (db.StockAdjustments.Any(adjustment =>
                    adjustment.AdjustmentType == ReversalType &&
                    adjustment.ReferenceDocumentType == normalizedSourceType &&
                    adjustment.ReferenceDocumentId == sourceId))
            {
                throw new InventoryDomainException("Chứng từ kho này đã được đảo trước đó.");
            }

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

            var warehouseId = GetSingleWarehouse(entries);
            EnsureSourceIsPosted(db, normalizedSourceType, sourceId, warehouseId);
            ValidateLedgerDirections(entries, normalizedSourceType);
            ApplyInverseBalances(db, entries, normalizedSourceType);
            RestoreSerials(db, normalizedSourceType, sourceId, warehouseId);

            var now = DateTime.UtcNow;
            var reversal = new StockAdjustment
            {
                DocumentCode = $"REV-{normalizedSourceType}-{sourceId}",
                WarehouseId = warehouseId,
                AdjustmentType = ReversalType,
                ReasonCode = "SYSTEM-REVERSAL",
                Status = "Posted",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = now,
                ReferenceDocumentType = normalizedSourceType,
                ReferenceDocumentId = sourceId,
                Notes = $"Reversal of {normalizedSourceType} #{sourceId}"
            };

            db.StockAdjustments.Add(reversal);
            db.SaveChanges();

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

            db.SaveChanges();
            transaction.Commit();
            return reversal.Id;
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryDomainException("Tồn kho vừa thay đổi. Vui lòng tải lại và thử lại.");
        }
    }

    public int ReversePostedLedgerDocument(string sourceType, int sourceId, int userId)
    {
        return ReverseDocument(sourceType, sourceId, userId);
    }

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

    private static void ApplyInverseBalances(
        AppDbContext db,
        IEnumerable<StockLedger> entries,
        string sourceType)
    {
        var movements = entries
            .GroupBy(entry => new { entry.ProductId, entry.WarehouseId })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.WarehouseId,
                Quantity = group.Sum(entry => entry.Quantity)
            });

        foreach (var movement in movements)
        {
            var balance = db.StockBalances.SingleOrDefault(item =>
                item.ProductId == movement.ProductId &&
                item.WarehouseId == movement.WarehouseId);

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
            var lineIds = lines.Select(line => line.Id).ToArray();
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
