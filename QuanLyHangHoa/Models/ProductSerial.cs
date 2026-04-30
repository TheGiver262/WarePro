using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class ProductSerial
    {
        public int Id { get; set; }
        
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
        
        [Required]
        [MaxLength(150)]
        public string SerialNumber { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string CurrentStatus { get; set; } = "InStock";
        
        public int? CurrentWarehouseId { get; set; }
        [ForeignKey("CurrentWarehouseId")]
        public virtual Warehouse? CurrentWarehouse { get; set; }
        
        public int LastStockInLineId { get; set; }
        [ForeignKey("LastStockInLineId")]
        public virtual StockInLine? LastStockInLine { get; set; }
        
        public int? LastStockOutLineId { get; set; }
        [ForeignKey("LastStockOutLineId")]
        public virtual StockOutLine? LastStockOutLine { get; set; }
        
        // Navigation to WarrantyCoverage
        public virtual WarrantyCoverage? WarrantyCoverage { get; set; }
    }
}
