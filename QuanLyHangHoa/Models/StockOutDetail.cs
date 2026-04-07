using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class StockOutDetail
    {
        public int Id { get; set; }
        
        public int StockOutId { get; set; }
        public virtual StockOut? StockOut { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; }
        public decimal ExportPrice { get; set; }
        
        // Cc serial ?c xut bn ra trong dng ny
        public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();
    }
}
