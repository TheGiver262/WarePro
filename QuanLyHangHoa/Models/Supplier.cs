using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();
    }
}
