using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class WarrantyClaim
{
    public int Id { get; set; }

    public string ClaimCode { get; set; } = null!;

    public int WarrantyCoverageId { get; set; }

    public int ProductSerialId { get; set; }

    public int? ReplacementSerialId { get; set; }

    public int? ReplacementStockOutId { get; set; }

    public DateTime ReceivedDate { get; set; }

    public string? ProblemDescription { get; set; }

    public string? TechnicalConclusion { get; set; }

    public string? ManufacturerResult { get; set; }

    public string? RejectionReason { get; set; }

    public string? ProcessingNote { get; set; }

    public string? ResolutionType { get; set; }

    public string Status { get; set; } = null!;

    public int? ApprovedBy { get; set; }

    public int ProcessedBy { get; set; }

    public DateTime? ClosedDate { get; set; }

    public virtual AppUser? Approver { get; set; }

    public virtual AppUser Processor { get; set; } = null!;

    public virtual ProductSerial ProductSerial { get; set; } = null!;

    public virtual ProductSerial? ReplacementSerial { get; set; }

    public virtual StockOut? ReplacementStockOut { get; set; }

    public virtual WarrantyCoverage WarrantyCoverage { get; set; } = null!;
}
