using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockOut
{
    public int Id { get; set; }

    public string DocumentCode { get; set; } = null!;

    public int CustomerId { get; set; }

    public int WarehouseId { get; set; }

    public string PurposeCode { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    public int? ApprovedBy { get; set; }

    public int? PostedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PostedAt { get; set; }

    public virtual AppUser? Approver { get; set; }

    public virtual AppUser Creator { get; set; } = null!;

    public virtual Customer Customer { get; set; } = null!;

    public virtual AppUser? Poster { get; set; }

    public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();

    public virtual ICollection<StockOutLine> Lines { get; set; } = new List<StockOutLine>();

    public virtual Warehouse Warehouse { get; set; } = null!;

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();
}
