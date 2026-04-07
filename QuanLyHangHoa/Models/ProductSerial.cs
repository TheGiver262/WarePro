using System;

namespace QuanLyHangHoa.Models
{
    public class ProductSerial
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        
        // Status: "InStock", "Sold", "Defective"
        public string Status { get; set; } = "InStock";
        public bool IsDeleted { get; set; } = false;

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int? StockInDetailId { get; set; }
        public virtual StockInDetail? StockInDetail { get; set; }

        public int? StockOutDetailId { get; set; }
        public virtual StockOutDetail? StockOutDetail { get; set; }

        public virtual Warranty? Warranty { get; set; }
    }
}
