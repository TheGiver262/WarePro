using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class StockLedger
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public int ProductId { get; set; }

    public int? ProductSerialId { get; set; }

    // SourceDocumentType + SourceDocumentId là khóa tham chiếu đa hình về chứng từ sinh biến động
    public string SourceDocumentType { get; set; } = null!;

    public int SourceDocumentId { get; set; }

    // Quantity luôn dương; MovementType In/Out quyết định cộng hay trừ khi tính số dư chạy
    public string MovementType { get; set; } = null!;

    public decimal Quantity { get; set; }

    public int PostedBy { get; set; }

    public DateTime PostedAt { get; set; }

    public virtual AppUser Poster { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;

    public virtual ProductSerial? ProductSerial { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
