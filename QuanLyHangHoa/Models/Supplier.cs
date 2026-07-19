using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Supplier
{
    public int Id { get; set; }

    public string SupplierCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();

    public virtual ICollection<StockIn> StockIns { get; set; } = new List<StockIn>();
    public byte[] RowVersion { get; set; } = [];

}
