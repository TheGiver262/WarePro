using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class SalesInvoice
{
    public int Id { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int CustomerId { get; set; }

    public int? StockOutId { get; set; }

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

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();

    public virtual StockOut? StockOut { get; set; }

    public virtual ICollection<WarrantyCoverage> WarrantyCoverages { get; set; } = new List<WarrantyCoverage>();
}
