using System;

namespace QuanLyHangHoa.Models;

public sealed class DocumentNumberCounter
{
    public string DocumentType { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public long LastValue { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
