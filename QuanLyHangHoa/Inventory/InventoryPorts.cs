using System;

namespace QuanLyHangHoa.Inventory;

/// <summary>
/// cổng để domain đọc snapshot và xếp thay đổi inventory vào cùng một commit.
/// </summary>
public interface IInventoryUnitOfWork
{
    bool CanApproveStock(int userId);
    bool CanProcessWarrantyStock(int userId);
    ProductSnapshot GetProduct(int productId);
    StockBalanceSnapshot? FindBalance(int productId, int warehouseId);
    StockBalanceSnapshot GetOrCreateBalance(int productId, int warehouseId);
    void SaveBalance(StockBalanceSnapshot balance);
    bool SerialExists(string serialNumber);
    ProductSerialSnapshot GetSerial(string serialNumber);
    void SaveSerial(ProductSerialSnapshot serial);
    void AddLedger(StockLedgerEntry entry);
    void AddAudit(AuditLogEntry entry);
    void MarkDocumentPosted(int documentId, string documentType);
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
