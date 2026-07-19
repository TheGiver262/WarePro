using System;

namespace QuanLyHangHoa.Models;

public sealed class WareProClientSession
{
    // id này định danh lease của một tiến trình; không dùng lại như id đăng nhập hay id người dùng.
    public Guid SessionId { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
    // token chống heartbeat/cleanup ghi đè lease vừa được tiến trình khác gia hạn.
    public byte[] RowVersion { get; set; } = [];
}
