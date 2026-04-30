using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockAdjustmentLine
    {
        public int Id { get; set; }

        public int AdjustmentId { get; set; }
        [ForeignKey("AdjustmentId")]
        public virtual StockAdjustment? StockAdjustment { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        public int? ProductSerialId { get; set; }
        [ForeignKey("ProductSerialId")]
        public virtual ProductSerial? ProductSerial { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal QuantityDelta { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseQuantityDelta { get; set; }
        
        [Required]
        [MaxLength(20)]
        public string Direction { get; set; } = string.Empty; // In, Out
    }
}
