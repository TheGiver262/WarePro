namespace QuanLyHangHoa.Models
{
    public class PurchaseInvoiceLine
    {
        public int Id { get; set; }

        public int PurchaseInvoiceId { get; set; }
        public virtual PurchaseInvoice? PurchaseInvoice { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int UnitId { get; set; }
        public virtual Unit? Unit { get; set; }

        public int? StockInDetailId { get; set; }
        public virtual StockInDetail? StockInDetail { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
    }
}
