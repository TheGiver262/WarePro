using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class PurchaseInvoice
{
    public int Id { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int SupplierId { get; set; }

    public int? StockInId { get; set; }

    public DateTime InvoiceDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public DateTime? DueDate { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }

    public virtual AppUser Creator { get; set; } = null!;

    public virtual ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();

    public virtual StockIn? StockIn { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
}
