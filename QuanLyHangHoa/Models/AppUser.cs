using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class AppUser
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string RoleCode { get; set; } = "Staff";
        
        public bool MustChangePassword { get; set; } = true;
        public int FailedLoginCount { get; set; } = 0;
        
        public int? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LockoutUntil { get; set; }
        public DateTime? LastFailedLoginAt { get; set; }
        public DateTime? LastPasswordChangedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        
        public bool IsActive { get; set; } = true;

        // Navigation properties
        [ForeignKey("CreatedBy")]
        public virtual AppUser? Creator { get; set; }
        
        [InverseProperty("Creator")]
        public virtual ICollection<StockIn>? CreatedStockIns { get; set; }
        
        [InverseProperty("Creator")]
        public virtual ICollection<StockOut>? CreatedStockOuts { get; set; }
        
        [InverseProperty("Poster")]
        public virtual ICollection<StockLedger>? PostedLedgers { get; set; }
    }
}
