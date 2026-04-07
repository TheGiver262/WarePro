using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models
{
    public class Unit
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
