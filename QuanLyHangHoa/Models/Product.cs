using System;
using System.Collections.Generic;
using System.Linq;

namespace QuanLyHangHoa.Models;

public partial class Product
{
    public int Id { get; set; }

    public string ProductCode { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal? CostPrice { get; set; }

    public int CategoryId { get; set; }

    public int BrandId { get; set; }

    public int DefaultUnitId { get; set; }

    public decimal DefaultPrice { get; set; }

    public string? OriginCountry { get; set; }

    public int WarrantyPeriodMonths { get; set; }

    public bool IsSerialTracked { get; set; }

    public bool IsActive { get; set; } = true;

    public string CategoryName => Category?.DisplayName ?? "N/A";
    public string BrandName => Brand?.DisplayName ?? "N/A";
    public string UnitName => DefaultUnit?.DisplayName ?? "N/A";
    public string ProductName => DisplayName;

    // các field dưới là projection hiển thị từ tổng OnHand mọi kho; giá trị tồn ưu tiên CostPrice
    public decimal StockQuantity => StockBalances?.Sum(b => b.OnHandQuantity) ?? 0;
    public bool IsLowStock => StockQuantity < 10; // Threshold for low stock
    public string Status => IsLowStock ? "lowstock" : "instock";
    public decimal TotalValue => StockQuantity * (CostPrice ?? DefaultPrice);

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
    public virtual ICollection<StockTransferLine> StockTransferLines { get; set; } = new List<StockTransferLine>();
    public byte[] RowVersion { get; set; } = [];

}
