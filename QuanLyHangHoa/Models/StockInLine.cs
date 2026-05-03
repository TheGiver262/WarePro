using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockInLine
{
    public int Id { get; set; }

    public int StockInId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal BaseQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();

    public virtual ICollection<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; } = new List<PurchaseInvoiceLine>();

    public virtual StockIn StockIn { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
