using System;

namespace QuanLyHangHoa.Inventory;

public sealed class InventoryDomainException : InvalidOperationException
{
    public InventoryDomainException(string message) : base(message)
    {
    }
}
