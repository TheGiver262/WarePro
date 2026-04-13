using System;
using System.Collections.Generic;

using QuanLyHangHoa.Services.DataImport;

namespace QuanLyHangHoa.Models
{
    public class Brand
    {
        public int Id { get; set; }
        [ImportKey]
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
