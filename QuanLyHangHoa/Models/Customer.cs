using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();
    }
}
