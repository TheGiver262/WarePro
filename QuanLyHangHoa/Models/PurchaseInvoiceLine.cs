using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class PurchaseInvoiceLine
{
    public int Id { get; set; }

    public int PurchaseInvoiceId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    // link nguồn khóa product, unit, quantity và unit price theo dòng nhập; hóa đơn chỉ bổ sung tax rate
    public int? StockInLineId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal GrandTotal { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual PurchaseInvoice PurchaseInvoice { get; set; } = null!;

    public virtual StockInLine? StockInLine { get; set; }

    public virtual Unit Unit { get; set; } = null!;
}
