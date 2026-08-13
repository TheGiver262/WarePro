namespace QuanLyHangHoa.Inventory;

public sealed class StaleEntityException : InventoryDomainException
{
    public StaleEntityException(string message) : base(message)
    {
    }
}
