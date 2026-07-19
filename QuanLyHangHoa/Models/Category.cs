using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Category
{
    public int Id { get; set; }

    public string CategoryCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public byte[] RowVersion { get; set; } = [];

}
