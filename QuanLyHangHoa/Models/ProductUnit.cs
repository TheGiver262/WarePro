using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyHangHoa.Models
{
    public class ProductUnit
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        public int UnitId { get; set; }
        [ForeignKey("UnitId")]
        public virtual Unit? Unit { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ConversionFactor { get; set; }
        
        public bool IsBaseUnit { get; set; } = false;
        public bool IsPurchaseUnit { get; set; } = false;
        public bool IsSalesUnit { get; set; } = false;
    }
}
