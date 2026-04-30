using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuanLyHangHoa.Models
{
    public class Warehouse
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string WarehouseCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string DisplayName { get; set; } = string.Empty;
        
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;

        public virtual ICollection<StockBalance>? StockBalances { get; set; }
        public virtual ICollection<ProductSerial>? ProductSerials { get; set; }
    }
}
