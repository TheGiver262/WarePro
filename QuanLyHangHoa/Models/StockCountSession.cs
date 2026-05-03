using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockCountSession
{
    public int Id { get; set; }

    public string SessionCode { get; set; } = null!;

    public int WarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public int CreatedBy { get; set; }

    public int? ApprovedBy { get; set; }

    public int? PostedBy { get; set; }

    public DateTime CountDate { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PostedAt { get; set; }

    public virtual AppUser? Approver { get; set; }

    public virtual AppUser Creator { get; set; } = null!;

    public virtual AppUser? Poster { get; set; }

    public virtual ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();

    public virtual Warehouse Warehouse { get; set; } = null!;
}
