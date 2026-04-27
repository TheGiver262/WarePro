namespace QuanLyHangHoa.Models
{
    public class StockBalance
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int WarehouseId { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        public int OnHandQuantity { get; set; }
        public int AvailableQuantity { get; set; }
        public int ReservedQuantity { get; set; }
    }
}
