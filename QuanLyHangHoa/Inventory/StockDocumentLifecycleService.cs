namespace QuanLyHangHoa.Inventory;

/// <summary>
/// giữ state machine chung để submit, approve, post và edit không đi tắt trạng thái.
/// </summary>
public sealed class StockDocumentLifecycleService
{
    // submit chốt nội dung draft để chuyển sang bước người có quyền phê duyệt.
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

    // kiểm tra permission trước transition để caller không nhận trạng thái Approved trái quyền.
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

    // chứng từ đã post phải sửa bằng reversal hoặc chứng từ bù; chỉnh trực tiếp sẽ phá ledger và audit trail.
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
