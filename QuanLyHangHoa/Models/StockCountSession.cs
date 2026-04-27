using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class StockCountSession
    {
        public int Id { get; set; }
        public string SessionCode { get; set; } = string.Empty;

        public int WarehouseId { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        public string Status { get; set; } = string.Empty;
        public DateTime CountDate { get; set; }
        public int CreatedBy { get; set; }
        public int? ApprovedBy { get; set; }
        public int? PostedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PostedAt { get; set; }

        public virtual ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();
    }
}
