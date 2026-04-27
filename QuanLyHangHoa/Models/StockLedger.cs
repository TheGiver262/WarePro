using System;

namespace QuanLyHangHoa.Models
{
    public class StockLedger
    {
        public int Id { get; set; }
        public Guid DocumentId { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int WarehouseId { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        public string Direction { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public DateTime PostedAt { get; set; }
        public int PostedByUserId { get; set; }
    }
}
