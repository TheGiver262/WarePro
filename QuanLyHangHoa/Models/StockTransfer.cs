using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockTransfer
{
    public int Id { get; set; }

    public string DocumentCode { get; set; } = null!;

    public int FromWarehouseId { get; set; }

    public int ToWarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime TransferDate { get; set; }

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

    public virtual Warehouse FromWarehouse { get; set; } = null!;

    public virtual Warehouse ToWarehouse { get; set; } = null!;

    public virtual ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
    public byte[] RowVersion { get; set; } = [];

}
