using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Models
{
    public class Supplier
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        [ImportKey]
        public string SupplierCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        [MaxLength(30)]
        public string? Phone { get; set; }
        
        [MaxLength(255)]
        public string? Email { get; set; }
        
        [MaxLength(500)]
        public string? Address { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public virtual ICollection<StockIn>? StockIns { get; set; }
        public virtual ICollection<PurchaseInvoice>? PurchaseInvoices { get; set; }
    }
}
