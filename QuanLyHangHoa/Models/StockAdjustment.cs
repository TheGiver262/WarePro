using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockAdjustment
{
    public int Id { get; set; }

    public string DocumentCode { get; set; } = null!;

    public int WarehouseId { get; set; }

    public string AdjustmentType { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? ReferenceDocumentType { get; set; }

    public int? ReferenceDocumentId { get; set; }

    public string ReasonCode { get; set; } = null!;

    public int CreatedBy { get; set; }

    public int? ApprovedBy { get; set; }

    public int? PostedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PostedAt { get; set; }

    public virtual AppUser? Approver { get; set; }

    public virtual AppUser Creator { get; set; } = null!;

    public virtual AppUser? Poster { get; set; }

    public string? ReferenceDocumentCode { get; set; }
    public virtual ICollection<StockAdjustmentLine> Lines { get; set; } = new List<StockAdjustmentLine>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}
