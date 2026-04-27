namespace QuanLyHangHoa.Models
{
    public class StockAdjustmentLine
    {
        public int Id { get; set; }

        public int StockAdjustmentId { get; set; }
        public virtual StockAdjustment? StockAdjustment { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int? ProductSerialId { get; set; }
        public virtual ProductSerial? ProductSerial { get; set; }

        public decimal QuantityDelta { get; set; }
        public decimal BaseQuantityDelta { get; set; }
        public string Direction { get; set; } = string.Empty;
    }
}
