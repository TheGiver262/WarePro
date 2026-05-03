using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class ProductSerial
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string SerialNumber { get; set; } = null!;

    public string CurrentStatus { get; set; } = null!;

    public int? CurrentWarehouseId { get; set; }

    public int LastStockInLineId { get; set; }

    public int? LastStockOutLineId { get; set; }

    public virtual Warehouse? CurrentWarehouse { get; set; }

    public virtual StockInLine LastStockInLine { get; set; } = null!;

    public virtual StockOutLine? LastStockOutLine { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<StockAdjustmentLine> StockAdjustmentLines { get; set; } = new List<StockAdjustmentLine>();

    public virtual ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();

    public virtual WarrantyClaim? WarrantyClaimProductSerial { get; set; }

    public virtual ICollection<WarrantyClaim> WarrantyClaimReplacementSerials { get; set; } = new List<WarrantyClaim>();

    public virtual WarrantyCoverage? WarrantyCoverage { get; set; }
}
