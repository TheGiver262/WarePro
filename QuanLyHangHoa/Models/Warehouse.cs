using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Warehouse
{
    public int Id { get; set; }

    public string WarehouseCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();

    public virtual ICollection<StockAdjustment> StockAdjustments { get; set; } = new List<StockAdjustment>();

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();

    public virtual ICollection<StockCountSession> StockCountSessions { get; set; } = new List<StockCountSession>();

    public virtual ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();

    public virtual ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();

    public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();
    public virtual ICollection<StockTransfer> StockTransfersFrom { get; set; } = new List<StockTransfer>();
    public virtual ICollection<StockTransfer> StockTransfersTo { get; set; } = new List<StockTransfer>();
}
