using System;

namespace QuanLyHangHoa.Models;

public sealed class AuditArchiveManifest
{
    public int Id { get; set; }
    public int ActorId { get; set; }
    public DateTime RangeStartUtc { get; set; }
    public DateTime RangeEndUtc { get; set; }
    public int RowCount { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public AppUser Actor { get; set; } = null!;
}