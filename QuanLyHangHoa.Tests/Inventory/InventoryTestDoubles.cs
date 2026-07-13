using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.Tests.Inventory;

internal sealed class InMemoryInventoryStore : IInventoryUnitOfWork
{
    public Dictionary<int, ProductSnapshot> Products { get; } = new();
    public Dictionary<(int ProductId, int WarehouseId), StockBalanceSnapshot> Balances { get; } = new();
    public Dictionary<string, ProductSerialSnapshot> Serials { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StockLedgerEntry> Ledgers { get; } = new();
    public List<AuditLogEntry> Audits { get; } = new();
    public Dictionary<(int Id, string Type), StockDocumentStatus> DocumentStatuses { get; } = new();
    public int PurchaseInvoiceCreatedCount { get; private set; }
    public bool WasCommitted { get; private set; }
    public bool IsStockApprovalAuthorized { get; set; } = true;
    public bool IsWarrantyStockAuthorized { get; set; } = true;

    public bool CanApproveStock(int userId) => IsStockApprovalAuthorized;
    public bool CanProcessWarrantyStock(int userId) => IsWarrantyStockAuthorized;

    public ProductSnapshot GetProduct(int productId)
    {
        return Products.TryGetValue(productId, out var product)
            ? product
            : throw new InventoryDomainException($"Product {productId} does not exist.");
    }

    public StockBalanceSnapshot? FindBalance(int productId, int warehouseId)
    {
        return Balances.TryGetValue((productId, warehouseId), out var balance)
            ? balance
            : null;
    }

    public StockBalanceSnapshot GetOrCreateBalance(int productId, int warehouseId)
    {
        var key = (productId, warehouseId);
        if (!Balances.TryGetValue(key, out var balance))
        {
            balance = new StockBalanceSnapshot(productId, warehouseId, 0, 0, 0);
            Balances[key] = balance;
        }

        return balance;
    }

    public void SaveBalance(StockBalanceSnapshot balance)
    {
        Balances[(balance.ProductId, balance.WarehouseId)] = balance;
    }

    public bool SerialExists(string serialNumber)
    {
        return Serials.ContainsKey(serialNumber);
    }

    public ProductSerialSnapshot GetSerial(string serialNumber)
    {
        return Serials.TryGetValue(serialNumber, out var serial)
            ? serial
            : throw new InventoryDomainException($"Serial {serialNumber} does not exist.");
    }

    public void SaveSerial(ProductSerialSnapshot serial)
    {
        Serials[serial.SerialNumber] = serial;
    }

    public void AddLedger(StockLedgerEntry entry)
    {
        Ledgers.Add(entry);
    }

    public void AddAudit(AuditLogEntry entry)
    {
        Audits.Add(entry);
    }

    public void MarkDocumentPosted(int documentId, string documentType)
    {
        DocumentStatuses[(documentId, documentType)] = StockDocumentStatus.Posted;
    }

    public void Commit()
    {
        WasCommitted = true;
    }
}

internal sealed class FixedWarehouseProvider : IDefaultWarehouseProvider
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

internal sealed class FixedClock : IClock
{
    public FixedClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; }
}
