using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class PurchaseInvoice
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string InvoiceCode { get; set; } = string.Empty;

        public int SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }

        public int? StockInId { get; set; }
        [ForeignKey("StockInId")]
        public virtual StockIn? StockIn { get; set; }

        public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; } = 0;
        
        [Required]
        [MaxLength(50)]
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Partial, Paid
        
        public DateTime DueDate { get; set; }

        public virtual ICollection<PurchaseInvoiceLine>? Lines { get; set; }
    }
}
