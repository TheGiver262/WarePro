using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Inventory;

/// <summary>
/// chuyển snapshot inventory sang entity EF; context do caller sở hữu và một SaveChanges là ranh giới commit.
/// </summary>
public sealed class EfInventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly bool _commitChanges;
    private readonly Dictionary<int, bool> _stockApprovalCache = [];
    private readonly HashSet<(int ProductId, int WarehouseId)> _loadedBalanceKeys = [];
    private readonly Dictionary<int, bool> _warrantyPermissionCache = [];
    private readonly HashSet<string> _loadedSerialNumbers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProductSerial> _serialTrackerIndex = new(StringComparer.OrdinalIgnoreCase);

    public EfInventoryUnitOfWork(AppDbContext context)
        : this(context, commitChanges: true)
    {
    }

    internal EfInventoryUnitOfWork(AppDbContext context, bool commitChanges)
    {
        // commitChanges = false khi service ngoÃ i Ä‘ang giá»¯ transaction; adapter chá»‰ stage entity Ä‘á»ƒ caller flush má»™t láº§n.
        _context = context;
        _commitChanges = commitChanges;
    }
    internal void MarkSerialsLoaded(IEnumerable<string> serialNumbers) =>
        _loadedSerialNumbers.UnionWith(serialNumbers.Select(Normalize));

    // Bulk-index entities đã preload vào EF Local; tránh O(N²) scan khi lookup từng serial lần đầu.
    internal void MarkSerialsLoaded(IEnumerable<ProductSerial> entities)
    {
        foreach (var e in entities)
        {
            var key = Normalize(e.SerialNumber);
            _serialTrackerIndex.TryAdd(key, e);
            _loadedSerialNumbers.Add(key);
        }
    }
    internal void MarkBalancesLoaded(IEnumerable<(int ProductId, int WarehouseId)> keys) =>
        _loadedBalanceKeys.UnionWith(keys);


    public bool CanApproveStock(int userId)
    {
        if (_stockApprovalCache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var actor = _context.AppUsers.AsNoTracking().SingleOrDefault(user => user.Id == userId);
        var allowed = AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock);
        _stockApprovalCache[userId] = allowed;
        return allowed;
    }

    public bool CanProcessWarrantyStock(int userId)
    {
        if (_warrantyPermissionCache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var actor = _context.AppUsers.AsNoTracking().SingleOrDefault(user => user.Id == userId);
        var allowed = AuthorizationService.CanPerform(actor, PermissionAction.CreateWarrantyClaim);
        _warrantyPermissionCache[userId] = allowed;
        return allowed;
    }

    public ProductSnapshot GetProduct(int productId)
    {
        var product = _context.Products.Local.SingleOrDefault(p => p.Id == productId && p.IsActive)
            ?? _context.Products.SingleOrDefault(p => p.Id == productId && p.IsActive);
        if (product is null)
        {
            throw new InventoryDomainException($"Product {productId} does not exist.");
        }

        return new ProductSnapshot(product.Id, product.IsSerialTracked);
    }

    // entity được track để EF giữ rowversion gốc; snapshot trả ra chỉ mang số dùng cho nghiệp vụ.
    public StockBalanceSnapshot? FindBalance(int productId, int warehouseId)
    {
        var balance = FindTrackedOrPersistedBalance(productId, warehouseId);

        return balance is null ? null : ToSnapshot(balance);
    }

    // ưu tiên entity đã track để nhiều dòng cùng giao dịch dùng chung một bản balance trong bộ nhớ.
    public StockBalanceSnapshot GetOrCreateBalance(int productId, int warehouseId)
    {
        var balance = FindTrackedOrPersistedBalance(productId, warehouseId);

        if (balance is null)
        {
            balance = new StockBalance
            {
                ProductId = productId,
                WarehouseId = warehouseId
            };
            _context.StockBalances.Add(balance);
        }

        return ToSnapshot(balance);
    }

    // snapshot chứa kết quả nghiệp vụ; entity chỉ được cập nhật tại adapter này.
    // cập nhật lại chính entity đã đọc để token concurrency so với đúng phiên bản tạo ra snapshot.
    public void SaveBalance(StockBalanceSnapshot snapshot)
    {
        var balance = FindTrackedOrPersistedBalance(snapshot.ProductId, snapshot.WarehouseId);

        if (balance is null)
        {
            balance = new StockBalance
            {
                ProductId = snapshot.ProductId,
                WarehouseId = snapshot.WarehouseId
            };
            _context.StockBalances.Add(balance);
        }

        balance.OnHandQuantity = snapshot.OnHandQuantity;
        balance.AvailableQuantity = snapshot.AvailableQuantity;
        balance.ReservedQuantity = snapshot.ReservedQuantity;
    }

    public bool SerialExists(string serialNumber)
    {
        return FindTrackedOrPersistedSerial(serialNumber) is not null;
    }

    public ProductSerialSnapshot GetSerial(string serialNumber)
    {
        var serial = FindTrackedOrPersistedSerial(serialNumber);

        if (serial is null)
        {
            throw new InventoryDomainException($"Serial {serialNumber} does not exist.");
        }

        return ToSnapshot(serial);
    }

    public void SaveSerial(ProductSerialSnapshot snapshot)
    {
        var normalizedSerial = Normalize(snapshot.SerialNumber);
        var serial = FindTrackedOrPersistedSerial(normalizedSerial);
        if (serial is null)
        {
            serial = new ProductSerial
            {
                SerialNumber = normalizedSerial,
                ProductId = snapshot.ProductId,
                // null cho serial từ Adjustment-In (không bắt nguồn từ phiếu nhập);
                // PostStockIn luôn truyền giá trị tường minh qua snapshot.StockInLineId.
                LastStockInLineId = snapshot.StockInLineId
            };
            _context.ProductSerials.Add(serial);
            _serialTrackerIndex[normalizedSerial] = serial;
        }

        serial.ProductId = snapshot.ProductId;
        serial.CurrentWarehouseId = snapshot.CurrentWarehouseId;
        serial.CurrentStatus = snapshot.Status.ToString();
        serial.StockTransferLineId = snapshot.StockTransferLineId;
    }

    // ledger là lịch sử bất biến của phát sinh; balance là số tổng hợp hiện tại.
    public void AddLedger(StockLedgerEntry entry)
    {
        _context.StockLedgers.Add(new StockLedger
        {
            SourceDocumentType = entry.SourceDocumentType,
            SourceDocumentId = entry.DocumentId,
            ProductId = entry.ProductId,
            WarehouseId = entry.WarehouseId,
            ProductSerialId = entry.ProductSerialId,
            MovementType = entry.Direction.ToString(),
            Quantity = entry.Quantity,
            PostedAt = entry.PostedAt,
            PostedBy = entry.PostedByUserId
        });
    }

    // audit được stage cùng transaction; chỉ ghi nhận posted khi ledger, balance và serial đều lưu thành công.
    public void AddAudit(AuditLogEntry entry)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = "InventoryDocument",
            EntityId = entry.DocumentId, 
            ActionCode = entry.ActionCode.ToString(),
            PerformedAt = entry.PerformedAt,
            PerformedBy = entry.PerformedByUserId
        });
    }

    // đổi trạng thái trên entity đang track để được lưu cùng các thay đổi inventory khác.
    public void MarkDocumentPosted(int documentId, string documentType)
    {
        switch (documentType)
        {
            case "StockIn":
                var stockIn = _context.StockIns.Find(documentId);
                if (stockIn != null) stockIn.Status = "Posted";
                break;
            case "StockOut":
                var stockOut = _context.StockOuts.Find(documentId);
                if (stockOut != null) stockOut.Status = "Posted";
                break;
            case "StockTransfer":
                var transfer = _context.StockTransfers.Find(documentId);
                if (transfer != null) transfer.Status = "Posted";
                break;
            case "StockAdjustment":
                var adj = _context.StockAdjustments.Find(documentId);
                if (adj != null) adj.Status = "Posted";
                break;
        }
    }

    // SaveChanges của EF gói toàn bộ lệnh trong một transaction; lỗi concurrency được đổi sang lỗi nghiệp vụ dễ xử lý.
    public void Commit()
    {
        if (!_commitChanges)
        {
            return;
        }

        // không retry bằng snapshot cũ; rowversion conflict phải được đẩy ra để caller tải lại dữ liệu mới.
        _context.SaveChanges();
    }

    private static StockBalanceSnapshot ToSnapshot(StockBalance balance)
    {
        return new StockBalanceSnapshot(
            balance.ProductId,
            balance.WarehouseId,
            balance.OnHandQuantity,
            balance.AvailableQuantity,
            balance.ReservedQuantity);
    }

    private static ProductSerialSnapshot ToSnapshot(ProductSerial serial)
    {
        if (!Enum.TryParse<SerialStatus>(serial.CurrentStatus, out var status))
        {
            throw new InventoryDomainException($"Serial {serial.SerialNumber} has unsupported status {serial.CurrentStatus}.");
        }

        return new ProductSerialSnapshot(
            serial.SerialNumber,
            serial.ProductId,
            serial.CurrentWarehouseId,
            status,
            serial.StockTransferLineId);
    }

    // điểm chuẩn hóa duy nhất: Trim + ToUpperInvariant → serial trong DB luôn là uppercase
    private static string Normalize(string serial) => serial.Trim().ToUpperInvariant();

    // tìm Local trước để không tạo hai entity cùng khóa khi balance vừa được thêm nhưng chưa commit.
    private ProductSerial? FindTrackedOrPersistedSerial(string serialNumber)
    {
        var key = Normalize(serialNumber);

        if (_serialTrackerIndex.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var tracked = _context.ProductSerials.Local
            .FirstOrDefault(serial => string.Equals(serial.SerialNumber, key, StringComparison.OrdinalIgnoreCase));

        if (tracked is not null)
        {
            _serialTrackerIndex[key] = tracked;
            return tracked;
        }

        if (_loadedSerialNumbers.Contains(key))
        {
            return null;
        }

        // DB query dùng normalized key (uppercase) — đảm bảo case-consistent dù DB collation.
        var dbSerial = _context.ProductSerials
            .FirstOrDefault(serial => serial.SerialNumber == key);

        if (dbSerial is not null)
        {
            _serialTrackerIndex[key] = dbSerial;
        }

        return dbSerial;
    }

    private StockBalance? FindTrackedOrPersistedBalance(int productId, int warehouseId)
    {
        var tracked = _context.StockBalances.Local
            .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);
        return tracked
            ?? (_loadedBalanceKeys.Contains((productId, warehouseId))
                ? null
                : _context.StockBalances.SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId));
    }
}
