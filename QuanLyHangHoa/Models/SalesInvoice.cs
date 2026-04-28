using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class SalesInvoice
    {
        public int Id { get; set; }
        public string InvoiceCode { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        public int? StockOutId { get; set; }
        public virtual StockOut? StockOut { get; set; }

        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }

        public virtual ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
    }
}
