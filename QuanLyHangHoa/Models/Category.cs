using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Models
{
    public class Category
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        [ImportKey]
        public string CategoryCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        public bool IsActive { get; set; } = true;
        
        public virtual ICollection<Product>? Products { get; set; }
    }
}
