using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class StockIn
    {
        public int Id { get; set; }
        public DateTime ImportDate { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsDeleted { get; set; } = false;

        public int EmployeeId { get; set; }
        public virtual Employee? Employee { get; set; }

        public int SupplierId { get; set; }
        public virtual Supplier? Supplier { get; set; }

        public virtual ICollection<StockInDetail> StockInDetails { get; set; } = new List<StockInDetail>();
    }
}
