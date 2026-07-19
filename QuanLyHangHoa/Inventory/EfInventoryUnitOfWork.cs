using System;
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

    public EfInventoryUnitOfWork(AppDbContext context)
        : this(context, commitChanges: true)
    {
    }

    internal EfInventoryUnitOfWork(AppDbContext context, bool commitChanges)
    {
        _context = context;
        _commitChanges = commitChanges;
    }

    public bool CanApproveStock(int userId)
    {
        var actor = _context.AppUsers.AsNoTracking().SingleOrDefault(user => user.Id == userId);
        return AuthorizationService.CanPerform(actor, PermissionAction.ApproveStock);
    }

    public bool CanProcessWarrantyStock(int userId)
    {
        var actor = _context.AppUsers.AsNoTracking().SingleOrDefault(user => user.Id == userId);
        return AuthorizationService.CanPerform(actor, PermissionAction.CreateWarrantyClaim);
    }

    public ProductSnapshot GetProduct(int productId)
    {
        var product = _context.Products.SingleOrDefault(p => p.Id == productId && p.IsActive);
        if (product is null)
        {
            throw new InventoryDomainException($"Product {productId} does not exist.");
        }

        return new ProductSnapshot(product.Id, product.IsSerialTracked);
    }

    public StockBalanceSnapshot? FindBalance(int productId, int warehouseId)
    {
        var balance = _context.StockBalances
            .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);

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
        return _context.ProductSerials.Any(s => s.SerialNumber == serialNumber);
    }

    public ProductSerialSnapshot GetSerial(string serialNumber)
    {
        var serial = _context.ProductSerials
            .SingleOrDefault(s => s.SerialNumber == serialNumber);

        if (serial is null)
        {
            throw new InventoryDomainException($"Serial {serialNumber} does not exist.");
        }

        return ToSnapshot(serial);
    }

    public void SaveSerial(ProductSerialSnapshot snapshot)
    {
        var serial = _context.ProductSerials.SingleOrDefault(s => s.SerialNumber == snapshot.SerialNumber);
        if (serial is null)
        {
            // schema cũ yêu cầu LastStockInLineId; lấy dòng nhập gần nhất của sản phẩm làm liên kết tương thích.
            var stockInLineId = _context.StockInLines
                .Where(l => l.ProductId == snapshot.ProductId)
                .OrderByDescending(l => l.Id)
                .Select(l => l.Id)
                .FirstOrDefault();

            if (stockInLineId == 0)
            {
                stockInLineId = _context.StockInLines.Select(l => l.Id).FirstOrDefault();
            }

            serial = new ProductSerial
            {
                SerialNumber = snapshot.SerialNumber,
                ProductId = snapshot.ProductId,
                LastStockInLineId = stockInLineId
            };
            _context.ProductSerials.Add(serial);
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

    // tìm Local trước để không tạo hai entity cùng khóa khi balance vừa được thêm nhưng chưa commit.
    private StockBalance? FindTrackedOrPersistedBalance(int productId, int warehouseId)
    {
        return _context.StockBalances.Local
            .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId)
            ?? _context.StockBalances
                .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);
    }
}
