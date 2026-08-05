using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class ProductSerial
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string SerialNumber { get; set; } = null!;

    // status serial chỉ thay qua nghiệp vụ kho/bảo hành; setter giữ nguyên mã ReturnedToManufacturer chuẩn
    private string _currentStatus = null!;
    public string CurrentStatus
    {
        get => _currentStatus;
        set => _currentStatus = string.Equals(value, "ReturnedToManufacturer", StringComparison.OrdinalIgnoreCase) 
            ? "ReturnedToManufacturer" 
            : (value ?? string.Empty);
    }

    public string? Note { get; set; }

    public int? CurrentWarehouseId { get; set; }

    // serial từ StockIn: có FK trỏ đến dòng phiếu nhập gốc; serial từ Adjustment-In: null (không có StockInLine)
    public int? LastStockInLineId { get; set; }

    public int? LastStockOutLineId { get; set; }
    public int? StockTransferLineId { get; set; }
    public virtual StockTransferLine? StockTransferLine { get; set; }

    public virtual Warehouse? CurrentWarehouse { get; set; }

    public virtual StockInLine? LastStockInLine { get; set; }

    public virtual StockOutLine? LastStockOutLine { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<StockAdjustmentLine> StockAdjustmentLines { get; set; } = new List<StockAdjustmentLine>();

    public virtual ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();

    public virtual ICollection<WarrantyClaim> WarrantyClaimReplacementSerials { get; set; } = new List<WarrantyClaim>();

    public virtual ICollection<WarrantyCoverage> WarrantyCoverages { get; set; } = new List<WarrantyCoverage>();
    public byte[] RowVersion { get; set; } = [];

}
