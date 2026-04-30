using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class WarrantyClaim
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ClaimCode { get; set; } = string.Empty;

        public int WarrantyCoverageId { get; set; }
        [ForeignKey("WarrantyCoverageId")]
        public virtual WarrantyCoverage? WarrantyCoverage { get; set; }

        public int ProductSerialId { get; set; }
        [ForeignKey("ProductSerialId")]
        public virtual ProductSerial? ProductSerial { get; set; }

        public int? ReplacementSerialId { get; set; }
        [ForeignKey("ReplacementSerialId")]
        public virtual ProductSerial? ReplacementSerial { get; set; }

        public int? ReplacementStockOutId { get; set; }
        [ForeignKey("ReplacementStockOutId")]
        public virtual StockOut? ReplacementStockOut { get; set; }

        public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
        
        [MaxLength(1000)]
        public string? ProblemDescription { get; set; }
        
        [MaxLength(1000)]
        public string? TechnicalConclusion { get; set; }
        
        [MaxLength(1000)]
        public string? ManufacturerResult { get; set; }
        
        [MaxLength(1000)]
        public string? RejectionReason { get; set; }
        
        [MaxLength(1000)]
        public string? ProcessingNote { get; set; }
        
        [MaxLength(50)]
        public string? ResolutionType { get; set; } // Repair, Replace, Refund, Reject
        
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Open"; // Open, Processing, ManufacturerWait, Ready, Closed, Rejected

        public int? ApprovedBy { get; set; }
        [ForeignKey("ApprovedBy")]
        public virtual AppUser? Approver { get; set; }
        
        public int ProcessedBy { get; set; }
        [ForeignKey("ProcessedBy")]
        public virtual AppUser? Processor { get; set; }
        
        public DateTime? ClosedDate { get; set; }
    }
}
