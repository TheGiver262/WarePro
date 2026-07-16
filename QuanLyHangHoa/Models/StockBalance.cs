using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockBalance
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    // Available = OnHand - Reserved; bản ghi được nhận diện bởi cặp WarehouseId + ProductId
    public decimal OnHandQuantity { get; set; }

    public decimal AvailableQuantity { get; set; }

    public decimal ReservedQuantity { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Warehouse Warehouse { get; set; } = null!;
}
