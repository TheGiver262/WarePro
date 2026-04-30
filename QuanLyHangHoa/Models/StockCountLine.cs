using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class StockCountLine
    {
        public int Id { get; set; }

        public int SessionId { get; set; }
        [ForeignKey("SessionId")]
        public virtual StockCountSession? StockCountSession { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SystemQuantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal CountedQuantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal VarianceQuantity { get; set; }
    }
}
