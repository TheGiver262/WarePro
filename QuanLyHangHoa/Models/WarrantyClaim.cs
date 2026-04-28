using System;

namespace QuanLyHangHoa.Models
{
    public class WarrantyClaim
    {
        public int Id { get; set; }
        public string ClaimCode { get; set; } = string.Empty;

        public int WarrantyCoverageId { get; set; }
        public virtual WarrantyCoverage? WarrantyCoverage { get; set; }

        public int ProductSerialId { get; set; }
        public virtual ProductSerial? ProductSerial { get; set; }

        public int? ReplacementSerialId { get; set; }
        public virtual ProductSerial? ReplacementSerial { get; set; }

        public int? ReplacementStockOutId { get; set; }
        public virtual StockOut? ReplacementStockOut { get; set; }

        public DateTime ReceivedDate { get; set; }
        public string ClaimStatus { get; set; } = string.Empty;
        public string ProblemDescription { get; set; } = string.Empty;
        public string TechnicalConclusion { get; set; } = string.Empty;
        public string ManufacturerResult { get; set; } = string.Empty;
        public string RejectionReason { get; set; } = string.Empty;
        public string ProcessingNote { get; set; } = string.Empty;
        public int? ApprovedBy { get; set; }
        public int? ProcessedBy { get; set; }
        public DateTime? ClosedDate { get; set; }
    }
}
