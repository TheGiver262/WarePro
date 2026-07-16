namespace QuanLyHangHoa.Inventory;

/// <summary>
/// trạng thái chuẩn trong domain trước khi ánh xạ sang chuỗi lưu trữ.
/// </summary>
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
    OpeningBalance,
    Adjustment,
    WarrantyReceive
}

public enum StockOutKind
{
    Sale,
    Adjustment,
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
    PostStockAdjustment,
    PostStockTransfer
}
