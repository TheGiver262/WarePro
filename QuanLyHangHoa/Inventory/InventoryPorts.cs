using System;

namespace QuanLyHangHoa.Inventory;

public interface IInventoryUnitOfWork
{
    ProductSnapshot GetProduct(int productId);
    StockBalanceSnapshot? FindBalance(int productId, int warehouseId);
    StockBalanceSnapshot GetOrCreateBalance(int productId, int warehouseId);
    void SaveBalance(StockBalanceSnapshot balance);
    bool SerialExists(string serialNumber);
    ProductSerialSnapshot GetSerial(string serialNumber);
    void SaveSerial(ProductSerialSnapshot serial);
    void AddLedger(StockLedgerEntry entry);
    void AddAudit(AuditLogEntry entry);
    void MarkDocumentPosted(Guid documentId);
    void Commit();
}

public interface IDefaultWarehouseProvider
{
    int GetDefaultWarehouseId();
}

public interface IClock
{
    DateTime Now { get; }
}
