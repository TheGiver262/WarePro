using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockIn
{
    public int Id { get; set; }

    public string DocumentCode { get; set; } = null!;

    public int? SupplierId { get; set; }

    public int WarehouseId { get; set; }

    public string PurposeCode { get; set; } = null!;

    public string Status { get; set; } = null!;
    public DateTime? ImportDate { get; set; }
    public string? Notes { get; set; }

    public int CreatedBy { get; set; }

    public int? ApprovedBy { get; set; }

    public int? PostedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PostedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    public virtual AppUser? Approver { get; set; }

    public virtual AppUser Creator { get; set; } = null!;

    public virtual AppUser? Poster { get; set; }

    public virtual ICollection<PurchaseInvoice> PurchaseInvoices { get; set; } = new List<PurchaseInvoice>();

    public virtual ICollection<StockInLine> Lines { get; set; } = new List<StockInLine>();

    public virtual Supplier? Supplier { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
