using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockBalance
    {
        public int Id { get; set; }

        public int WarehouseId { get; set; }
        [ForeignKey("WarehouseId")]
        public virtual Warehouse? Warehouse { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal OnHandQuantity { get; set; } = 0;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal AvailableQuantity { get; set; } = 0;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReservedQuantity { get; set; } = 0;
    }
}
