using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class ProductUnit
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    // số đơn vị cơ sở tương ứng với một đơn vị này; phải lớn hơn 0 và base unit có hệ số 1
    public decimal ConversionFactor { get; set; }

    public bool IsBaseUnit { get; set; }

    public bool IsPurchaseUnit { get; set; }

    public bool IsSalesUnit { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];

}
