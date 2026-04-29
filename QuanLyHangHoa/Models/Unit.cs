using System;
using System.Collections.Generic;

using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Models
{
    public class Unit
    {
        public int Id { get; set; }
        [ImportKey]
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
        public virtual ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();
    }
}
