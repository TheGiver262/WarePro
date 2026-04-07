using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class StockInDetail
    {
        public int Id { get; set; }
        
        public int StockInId { get; set; }
        public virtual StockIn? StockIn { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int Quantity { get; set; }
        public decimal ImportPrice { get; set; }
        
        public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();
    }
}
