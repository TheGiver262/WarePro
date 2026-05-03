using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Brand
{
    public int Id { get; set; }

    public string BrandCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? OriginCountry { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
