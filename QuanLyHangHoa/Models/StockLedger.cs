using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockLedger
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    public int? ProductSerialId { get; set; }

    public string SourceDocumentType { get; set; } = null!;

    public int SourceDocumentId { get; set; }

    public string MovementType { get; set; } = null!;

    public decimal Quantity { get; set; }

    public int PostedBy { get; set; }

    public DateTime PostedAt { get; set; }

    public virtual AppUser Poster { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductSerial? ProductSerial { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
