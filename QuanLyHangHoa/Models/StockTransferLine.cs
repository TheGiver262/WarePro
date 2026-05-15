using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockTransferLine
{
    public int Id { get; set; }

    public int StockTransferId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal BaseQuantity { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual StockTransfer StockTransfer { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;

    public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();
}
