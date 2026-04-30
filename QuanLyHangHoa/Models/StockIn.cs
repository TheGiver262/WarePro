using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockIn
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string DocumentCode { get; set; } = string.Empty;
        
        public int? SupplierId { get; set; }
        [ForeignKey("SupplierId")]
        public virtual Supplier? Supplier { get; set; }
        
        public int WarehouseId { get; set; }
        [ForeignKey("WarehouseId")]
        public virtual Warehouse? Warehouse { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string PurposeCode { get; set; } = "Purchase"; // Purchase, OpeningBalance
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Approved, Posted
        
        public int CreatedBy { get; set; }
        [ForeignKey("CreatedBy")]
        public virtual AppUser? Creator { get; set; }
        
        public int? ApprovedBy { get; set; }
        [ForeignKey("ApprovedBy")]
        public virtual AppUser? Approver { get; set; }
        
        public int? PostedBy { get; set; }
        [ForeignKey("PostedBy")]
        public virtual AppUser? Poster { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PostedAt { get; set; }

        public virtual ICollection<StockInLine> Lines { get; set; } = new List<StockInLine>();
    }
}
