namespace QuanLyHangHoa.Models
{
    public class ProductUnit
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public virtual Product? Product { get; set; }

        public int UnitId { get; set; }
        public virtual Unit? Unit { get; set; }

        public decimal ConversionRateToBaseUnit { get; set; }
        public bool IsBaseUnit { get; set; }
        public bool IsDeleted { get; set; }
    }
}
