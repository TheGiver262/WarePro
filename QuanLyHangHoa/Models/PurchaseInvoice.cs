using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class PurchaseInvoice
    {
        public int Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }

        public int SupplierId { get; set; }
        public virtual Supplier? Supplier { get; set; }

        public int? StockInId { get; set; }
        public virtual StockIn? StockIn { get; set; }

        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }

        public virtual ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
    }
}
