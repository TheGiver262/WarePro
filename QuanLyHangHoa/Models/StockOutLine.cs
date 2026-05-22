using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockOutLine
{
    public int Id { get; set; }

    public int StockOutId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal BaseQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public string? DraftSerials { get; set; }

    public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();

    public virtual ICollection<SalesInvoiceLine> SalesInvoiceLines { get; set; } = new List<SalesInvoiceLine>();

    public virtual StockOut StockOut { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
