using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class StockAdjustment
    {
        public int Id { get; set; }
        public string DocumentCode { get; set; } = string.Empty;

        public int WarehouseId { get; set; }
        public virtual Warehouse? Warehouse { get; set; }

        public string AdjustmentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ReferenceDocumentCode { get; set; } = string.Empty;
        public string ReasonCode { get; set; } = string.Empty;
        public int CreatedBy { get; set; }
        public int? ApprovedBy { get; set; }
        public int? PostedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PostedAt { get; set; }

        public virtual ICollection<StockAdjustmentLine> Lines { get; set; } = new List<StockAdjustmentLine>();
    }
}
