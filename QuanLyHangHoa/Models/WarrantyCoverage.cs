using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class WarrantyCoverage
{
    public int Id { get; set; }

    public int ProductSerialId { get; set; }

    public int CustomerId { get; set; }

    public int? SalesInvoiceId { get; set; }

    // coverage là snapshot theo serial/khách/hóa đơn; khi thay serial chỉ chuyển phần thời hạn còn lại
    public DateTime WarrantyStartDate { get; set; }

    public DateTime WarrantyEndDate { get; set; }

    public string CoverageStatus { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    // field hiển thị không lưu database; Expired được suy ra theo ngày hiện tại
    public string EffectiveCoverageStatus { get; set; } = string.Empty;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ProductSerial ProductSerial { get; set; } = null!;

    public virtual SalesInvoice? SalesInvoice { get; set; }

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();
}
