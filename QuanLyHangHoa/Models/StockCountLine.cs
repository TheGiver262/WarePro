namespace QuanLyHangHoa.Models
{
    public class StockCountLine
    {
        public int Id { get; set; }

        public int StockCountSessionId { get; set; }
        public virtual StockCountSession? StockCountSession { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public decimal SystemQuantity { get; set; }
        public decimal CountedQuantity { get; set; }
        public decimal DifferenceQuantity { get; set; }
    }
}
