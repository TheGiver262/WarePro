using System;

namespace QuanLyHangHoa.Models;

public sealed class WareProClientSession
{
    public Guid SessionId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
