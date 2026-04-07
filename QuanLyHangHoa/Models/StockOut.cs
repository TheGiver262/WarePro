using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class StockOut
    {
        public int Id { get; set; }
        public DateTime ExportDate { get; set; }
        public decimal TotalAmount { get; set; }
        public bool IsDeleted { get; set; } = false;

        public int EmployeeId { get; set; }
        public virtual Employee? Employee { get; set; }

        public int CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }

        public virtual ICollection<StockOutDetail> StockOutDetails { get; set; } = new List<StockOutDetail>();
    }
}
