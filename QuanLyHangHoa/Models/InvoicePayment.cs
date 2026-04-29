using System;

namespace QuanLyHangHoa.Models
{
    public class InvoicePayment
    {
        public int Id { get; set; }

        public int? SalesInvoiceId { get; set; }
        public virtual SalesInvoice? SalesInvoice { get; set; }

        public int? PurchaseInvoiceId { get; set; }
        public virtual PurchaseInvoice? PurchaseInvoice { get; set; }

        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public int ReceivedBy { get; set; }
    }
}
