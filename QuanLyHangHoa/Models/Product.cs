using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class Product
{
    public int Id { get; set; }

    public string ProductCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public int CategoryId { get; set; }

    public int BrandId { get; set; }

    public int DefaultUnitId { get; set; }

    public decimal DefaultPrice { get; set; }

    public string? OriginCountry { get; set; }

    public int WarrantyPeriodMonths { get; set; }

    public bool IsSerialTracked { get; set; }

    public bool IsActive { get; set; }

    public virtual Brand Brand { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;

    public virtual Unit DefaultUnit { get; set; } = null!;

    public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();

    public virtual ProductUnit? ProductUnit { get; set; }

    public virtual ICollection<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; } = new List<PurchaseInvoiceLine>();

    public virtual ICollection<SalesInvoiceLine> SalesInvoiceLines { get; set; } = new List<SalesInvoiceLine>();

    public virtual ICollection<StockAdjustmentLine> StockAdjustmentLines { get; set; } = new List<StockAdjustmentLine>();

    public virtual ICollection<StockBalance> StockBalances { get; set; } = new List<StockBalance>();

    public virtual ICollection<StockCountLine> StockCountLines { get; set; } = new List<StockCountLine>();

    public virtual ICollection<StockInLine> StockInLines { get; set; } = new List<StockInLine>();

    public virtual ICollection<StockLedger> StockLedgers { get; set; } = new List<StockLedger>();

    public virtual ICollection<StockOutLine> StockOutLines { get; set; } = new List<StockOutLine>();
}
