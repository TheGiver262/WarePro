using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockOut
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string DocumentCode { get; set; } = string.Empty;
        
        public int CustomerId { get; set; }
        [ForeignKey("CustomerId")]
        public virtual Customer? Customer { get; set; }
        
        public int WarehouseId { get; set; }
        [ForeignKey("WarehouseId")]
        public virtual Warehouse? Warehouse { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string PurposeCode { get; set; } = "Sale"; // Sale, WarrantyReplacement
        
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

        public virtual ICollection<StockOutLine> Lines { get; set; } = new List<StockOutLine>();
    }
}
