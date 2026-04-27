using System;

namespace QuanLyHangHoa.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public Guid DocumentId { get; set; }
        public string ActionCode { get; set; } = string.Empty;
        public DateTime PerformedAt { get; set; }
        public int PerformedByUserId { get; set; }
    }
}
