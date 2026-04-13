using System;
using System.Collections.Generic;
using QuanLyHangHoa.Services.DataImport;
namespace QuanLyHangHoa.Models
{
    public class Customer
    {
        public int Id { get; set; }
        [ImportKey]
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        [ImportKey]
        public string Phone { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();
    }
}
