using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockCountLine
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int ProductId { get; set; }

    public decimal SystemQuantity { get; set; }

    public decimal CountedQuantity { get; set; }

    public decimal VarianceQuantity { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual StockCountSession Session { get; set; } = null!;
}
