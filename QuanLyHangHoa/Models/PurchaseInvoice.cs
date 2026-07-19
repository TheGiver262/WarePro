using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class PurchaseInvoice
{
    public int Id { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public int SupplierId { get; set; }

    // có StockInId thì dòng hóa đơn phải khớp tuyệt đối phiếu nhập Purchase đã posted
    public int? StockInId { get; set; }

    public DateTime InvoiceDate { get; set; }

    // các tổng tiền dùng decimal và luôn được InvoiceService tính lại từ Lines
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

    public virtual ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();

    public virtual StockIn? StockIn { get; set; }

    public virtual Supplier Supplier { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];

}
