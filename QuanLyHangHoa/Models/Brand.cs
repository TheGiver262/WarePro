using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Models
{
    public class Brand
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        [ImportKey]
        public string BrandCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? OriginCountry { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public virtual ICollection<Product>? Products { get; set; }
    }
}
