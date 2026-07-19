using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Unit
{
    public int Id { get; set; }

    public string UnitCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<ProductUnit> ProductUnits { get; set; } = new List<ProductUnit>();

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    public virtual ICollection<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; } = new List<PurchaseInvoiceLine>();

    public virtual ICollection<SalesInvoiceLine> SalesInvoiceLines { get; set; } = new List<SalesInvoiceLine>();

    public virtual ICollection<StockInLine> StockInLines { get; set; } = new List<StockInLine>();

    public virtual ICollection<StockOutLine> StockOutLines { get; set; } = new List<StockOutLine>();
    public virtual ICollection<StockTransferLine> StockTransferLines { get; set; } = new List<StockTransferLine>();
    public byte[] RowVersion { get; set; } = [];

}
