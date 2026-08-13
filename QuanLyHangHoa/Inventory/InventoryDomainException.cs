using System;

namespace QuanLyHangHoa.Inventory;

/// <summary>
/// lỗi vi phạm quy tắc tồn kho có thể trả cho caller mà không lộ lỗi hạ tầng.
/// </summary>
public class InventoryDomainException : InvalidOperationException
{
    public InventoryDomainException(string message) : base(message)
    {
    }

    public InventoryDomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
