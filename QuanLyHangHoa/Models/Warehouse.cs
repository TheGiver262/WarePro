using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class Warehouse
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();
        public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();
    }
}
