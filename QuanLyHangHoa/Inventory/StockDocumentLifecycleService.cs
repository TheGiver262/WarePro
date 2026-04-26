namespace QuanLyHangHoa.Inventory;

public sealed class StockDocumentLifecycleService
{
    public StockDocumentStatus SubmitForApproval(StockDocumentStatus current)
    {
        if (current != StockDocumentStatus.Draft)
        {
            throw new InventoryDomainException("Only draft documents can be submitted for approval.");
        }

        return StockDocumentStatus.PendingApproval;
    }

    public StockDocumentStatus Approve(StockDocumentStatus current)
    {
        if (current != StockDocumentStatus.PendingApproval)
        {
            throw new InventoryDomainException("Only pending documents can be approved.");
        }

        return StockDocumentStatus.Approved;
    }

    public void EnsureCanEditDetails(StockDocumentStatus current)
    {
        if (current == StockDocumentStatus.Posted)
        {
            throw new InventoryDomainException("Posted documents cannot be edited directly.");
        }

        if (current == StockDocumentStatus.Locked)
        {
            throw new InventoryDomainException("Locked documents cannot be edited.");
        }
    }
}
