using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Customer
{
    public int Id { get; set; }

    public string CustomerCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }
    public string? Address { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();

    public virtual ICollection<StockOut> StockOuts { get; set; } = new List<StockOut>();

    public virtual ICollection<WarrantyCoverage> WarrantyCoverages { get; set; } = new List<WarrantyCoverage>();
    public byte[] RowVersion { get; set; } = [];

}
