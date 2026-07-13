using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Inventory;

public sealed class EfInventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly AppDbContext _context;

    public EfInventoryUnitOfWork(AppDbContext context)
    {
        _context = context;
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

    public void Commit()
    {
        try
        {
            _context.SaveChanges();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InventoryDomainException("Tồn kho vừa thay đổi. Vui lòng tải lại và thử lại.");
        }
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

    private StockBalance? FindTrackedOrPersistedBalance(int productId, int warehouseId)
    {
        return _context.StockBalances.Local
            .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId)
            ?? _context.StockBalances
                .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);
    }
}
