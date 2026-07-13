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
        return Approve(current, isAuthorized: true);
    }

    public StockDocumentStatus Approve(StockDocumentStatus current, bool isAuthorized)
    {
        if (!isAuthorized)
        {
            throw new InventoryDomainException("You are not authorized to approve stock documents.");
        }

        if (current != StockDocumentStatus.PendingApproval)
        {
            throw new InventoryDomainException("Only pending documents can be approved.");
        }

        return StockDocumentStatus.Approved;
    }

    public void EnsureCanPost(StockDocumentStatus current)
    {
        if (current != StockDocumentStatus.Approved)
        {
            throw new InventoryDomainException("Only approved documents can be posted.");
        }
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

        if (current != StockDocumentStatus.Draft)
        {
            throw new InventoryDomainException("Only draft documents can be edited.");
        }
    }
}
