using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockAdjustment
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string DocumentCode { get; set; } = string.Empty;

        public int WarehouseId { get; set; }
        [ForeignKey("WarehouseId")]
        public virtual Warehouse? Warehouse { get; set; }

        [Required]
        [MaxLength(50)]
        public string AdjustmentType { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Draft"; // Draft, Approved, Posted, Cancelled
        
        [MaxLength(50)]
        public string? ReferenceDocumentType { get; set; }
        
        public int? ReferenceDocumentId { get; set; }
        
        [MaxLength(50)]
        public string? ReferenceDocumentCode { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ReasonCode { get; set; } = string.Empty;

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

        public virtual ICollection<StockAdjustmentLine>? Lines { get; set; }
    }
}
