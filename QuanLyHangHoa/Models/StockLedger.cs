using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockLedger
    {
        public int Id { get; set; }
        
        public int WarehouseId { get; set; }
        [ForeignKey("WarehouseId")]
        public virtual Warehouse? Warehouse { get; set; }
        
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }
        
        public int? ProductSerialId { get; set; }
        [ForeignKey("ProductSerialId")]
        public virtual ProductSerial? ProductSerial { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string SourceDocumentType { get; set; } = string.Empty; // StockIn, StockOut, StockAdjustment
        
        public int SourceDocumentId { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string MovementType { get; set; } = string.Empty; // In, Out
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Quantity { get; set; }
        
        public int PostedBy { get; set; }
        [ForeignKey("PostedBy")]
        public virtual AppUser? Poster { get; set; }
        
        public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    }
}
