namespace QuanLyHangHoa.Inventory;

public enum StockDocumentStatus
{
    Draft,
    PendingApproval,
    Approved,
    Posted,
    Locked,
    Cancelled
}

public enum StockInKind
{
    Purchase,
    OpeningBalance
}

public enum StockOutKind
{
    Sale,
    WarrantyReplacement
}

public enum SerialStatus
{
    InStock,
    Reserved,
    Sold,
    InWarrantyProcess,
    WarrantyDefective,
    ReturnedToManufacturer,
    Replaced,
    Inactive
}

public enum StockLedgerDirection
{
    In,
    Out
}

public enum AuditActionCode
{
    PostStockIn,
    PostStockOut,
    PostStockAdjustment
}
