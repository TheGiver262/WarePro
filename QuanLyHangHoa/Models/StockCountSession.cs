using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockCountSession
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string SessionCode { get; set; } = string.Empty;

        public int WarehouseId { get; set; }
        [ForeignKey("WarehouseId")]
        public virtual Warehouse? Warehouse { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Counting, Completed, Posted, Cancelled
        
        public DateTime CountDate { get; set; } = DateTime.UtcNow;

        public int CreatedBy { get; set; }
        [ForeignKey("CreatedBy")]
        public virtual AppUser? Creator { get; set; }
        
        public int? ApprovedBy { get; set; }
        [ForeignKey("ApprovedBy")]
        public virtual AppUser? Approver { get; set; }
        
        public int? PostedBy { get; set; }
        [ForeignKey("PostedBy")]
        public virtual AppUser? Poster { get; set; }
        
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PostedAt { get; set; }

        public virtual ICollection<StockCountLine>? Lines { get; set; }
    }
}
