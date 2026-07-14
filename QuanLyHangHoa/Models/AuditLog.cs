using System;
using System.Collections.Generic;

namespace QuanLyHangHoa.Models;

public partial class AuditLog
{
    public int Id { get; set; }

    public string EntityName { get; set; } = null!;

    public int EntityId { get; set; }

    public string ActionCode { get; set; } = null!;

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public int? PerformedBy { get; set; }

    public DateTime PerformedAt { get; set; }

    public virtual AppUser? Performer { get; set; }
}
