using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;
        
        public int EntityId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string ActionCode { get; set; } = string.Empty;
        
        public string? BeforeJson { get; set; }
        public string? AfterJson { get; set; }
        
        public int PerformedBy { get; set; }
        [ForeignKey("PerformedBy")]
        public virtual AppUser? Performer { get; set; }
        
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }
}
