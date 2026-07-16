using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class SalesInvoice
{
    public int Id { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int CustomerId { get; set; }

    // có StockOutId thì hóa đơn gắn duy nhất một phiếu xuất Sale đã posted và cùng khách hàng
    public int? StockOutId { get; set; }

    public DateTime InvoiceDate { get; set; }

    // InvoiceService tính lại subtotal, tax, grand total và payment status trước khi lưu
    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public string PaymentStatus { get; set; } = global::QuanLyHangHoa.Models.PaymentStatus.Unpaid;
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
