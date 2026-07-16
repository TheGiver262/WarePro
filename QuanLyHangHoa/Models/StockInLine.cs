using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockInLine
{
    public int Id { get; set; }

    public int StockInId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public decimal Quantity { get; set; }

    // BaseQuantity = Quantity * conversion factor; ledger và số serial dùng giá trị này
    public decimal BaseQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    // chuỗi serial là snapshot khi còn draft; sau post, ProductSerial là nguồn trạng thái chính
    public string? DraftSerials { get; set; }

    public virtual ICollection<ProductSerial> ProductSerials { get; set; } = new List<ProductSerial>();

    public virtual ICollection<PurchaseInvoiceLine> PurchaseInvoiceLines { get; set; } = new List<PurchaseInvoiceLine>();

    public virtual StockIn StockIn { get; set; } = null!;

    public virtual Unit Unit { get; set; } = null!;
}
