using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class ProductUnit
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public decimal ConversionFactor { get; set; }

    public bool IsBaseUnit { get; set; }

    public bool IsPurchaseUnit { get; set; }

    public bool IsSalesUnit { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
