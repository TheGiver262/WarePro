using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class SalesInvoiceLine
{
    public int Id { get; set; }

    public int SalesInvoiceId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public int? StockOutLineId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal GrandTotal { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual SalesInvoice SalesInvoice { get; set; } = null!;

    public virtual StockOutLine? StockOutLine { get; set; }

    public virtual Unit Unit { get; set; } = null!;
}
