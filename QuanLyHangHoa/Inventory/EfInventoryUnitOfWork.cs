using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Inventory;

public sealed class EfInventoryUnitOfWork : IInventoryUnitOfWork
{
    private readonly AppDbContext _context;

    public EfInventoryUnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public ProductSnapshot GetProduct(int productId)
    {
        var product = _context.Products.SingleOrDefault(p => p.Id == productId && !p.IsDeleted);
        if (product is null)
        {
            throw new InventoryDomainException($"Product {productId} does not exist.");
        }

        return new ProductSnapshot(product.Id, product.IsSerialManaged);
    }

    public StockBalanceSnapshot? FindBalance(int productId, int warehouseId)
    {
        var balance = _context.StockBalances
            .AsNoTracking()
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
        return _context.ProductSerials.Any(s => s.SerialNumber == serialNumber && !s.IsDeleted);
    }

    public ProductSerialSnapshot GetSerial(string serialNumber)
    {
        var serial = _context.ProductSerials
            .SingleOrDefault(s => s.SerialNumber == serialNumber && !s.IsDeleted);

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
            serial = new ProductSerial
            {
                SerialNumber = snapshot.SerialNumber,
                ProductId = snapshot.ProductId
            };
            _context.ProductSerials.Add(serial);
        }

        serial.ProductId = snapshot.ProductId;
        serial.CurrentWarehouseId = snapshot.CurrentWarehouseId;
        serial.Status = snapshot.Status.ToString();
        serial.IsDeleted = false;
    }

    public void AddLedger(StockLedgerEntry entry)
    {
        _context.StockLedgers.Add(new StockLedger
        {
            DocumentId = entry.DocumentId,
            ProductId = entry.ProductId,
            WarehouseId = entry.WarehouseId,
            Direction = entry.Direction.ToString(),
            Quantity = entry.Quantity,
            PostedAt = entry.PostedAt,
            PostedByUserId = entry.PostedByUserId
        });
    }

    public void AddAudit(AuditLogEntry entry)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            DocumentId = entry.DocumentId,
            ActionCode = entry.ActionCode.ToString(),
            PerformedAt = entry.PerformedAt,
            PerformedByUserId = entry.PerformedByUserId
        });
    }

    public void MarkDocumentPosted(Guid documentId)
    {
    }

    public void Commit()
    {
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
        if (!Enum.TryParse<SerialStatus>(serial.Status, out var status))
        {
            throw new InventoryDomainException($"Serial {serial.SerialNumber} has unsupported status {serial.Status}.");
        }

        return new ProductSerialSnapshot(
            serial.SerialNumber,
            serial.ProductId,
            serial.CurrentWarehouseId,
            status);
    }

    private StockBalance? FindTrackedOrPersistedBalance(int productId, int warehouseId)
    {
        return _context.StockBalances.Local
            .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId)
            ?? _context.StockBalances
                .SingleOrDefault(b => b.ProductId == productId && b.WarehouseId == warehouseId);
    }
}
