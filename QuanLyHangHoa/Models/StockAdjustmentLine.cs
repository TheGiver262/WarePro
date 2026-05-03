using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockAdjustmentLine
{
    public int Id { get; set; }

    public int AdjustmentId { get; set; }

    public int ProductId { get; set; }

    public int? ProductSerialId { get; set; }

    public decimal QuantityDelta { get; set; }

    public decimal BaseQuantityDelta { get; set; }

    public string Direction { get; set; } = null!;

    public virtual StockAdjustment Adjustment { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductSerial? ProductSerial { get; set; }
}
