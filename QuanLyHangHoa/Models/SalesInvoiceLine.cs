namespace QuanLyHangHoa.Models
{
    public class SalesInvoiceLine
    {
        public int Id { get; set; }

        public int SalesInvoiceId { get; set; }
        public virtual SalesInvoice? SalesInvoice { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int UnitId { get; set; }
        public virtual Unit? Unit { get; set; }

        public int? StockOutDetailId { get; set; }
        public virtual StockOutDetail? StockOutDetail { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
    }
}
