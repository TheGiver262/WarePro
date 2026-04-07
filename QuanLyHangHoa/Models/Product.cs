using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        public int CategoryId { get; set; }
        public virtual Category? Category { get; set; }

        public int BrandId { get; set; }
        public virtual Brand? Brand { get; set; }

        public int UnitId { get; set; }
        public virtual Unit? Unit { get; set; }
        
        // Stock level calculated from ProductSerial count in stock
        public int Quantity { get; set; }
        
        public decimal UnitPrice { get; set; }
        public string Origin { get; set; } = string.Empty;
        public int WarrantyMonths { get; set; }
        public string Notes { get; set; } = string.Empty;

        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();
    }
}
