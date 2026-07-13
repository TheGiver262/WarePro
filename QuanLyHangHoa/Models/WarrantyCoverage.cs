using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class WarrantyCoverage
{
    public int Id { get; set; }

    public int ProductSerialId { get; set; }

    public int CustomerId { get; set; }

    public int? SalesInvoiceId { get; set; }

    public DateTime WarrantyStartDate { get; set; }

    public DateTime WarrantyEndDate { get; set; }

    public string CoverageStatus { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string EffectiveCoverageStatus { get; set; } = string.Empty;

    public virtual Customer Customer { get; set; } = null!;

    public virtual ProductSerial ProductSerial { get; set; } = null!;

    public virtual SalesInvoice? SalesInvoice { get; set; }

    public virtual ICollection<WarrantyClaim> WarrantyClaims { get; set; } = new List<WarrantyClaim>();
}
