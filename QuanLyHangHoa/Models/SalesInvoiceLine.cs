using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class SalesInvoiceLine
    {
        public int Id { get; set; }

        public int SalesInvoiceId { get; set; }
        [ForeignKey("SalesInvoiceId")]
        public virtual SalesInvoice? SalesInvoice { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        public int UnitId { get; set; }
        [ForeignKey("UnitId")]
        public virtual Unit? Unit { get; set; }

        public int? StockOutLineId { get; set; }
        [ForeignKey("StockOutLineId")]
        public virtual StockOutLine? StockOutLine { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }
        
        [Column(TypeName = "decimal(9,4)")]
        public decimal TaxRate { get; set; } = 0;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxAmount { get; set; } = 0;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }
    }
}
