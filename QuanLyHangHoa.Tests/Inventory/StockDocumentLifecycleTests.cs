using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class StockDocumentLifecycleTests
{
    [Fact]
    public void SubmitDraftStockDocument_moves_document_to_pending_approval()
    {
        var service = new StockDocumentLifecycleService();

        var next = service.SubmitForApproval(StockDocumentStatus.Draft);

        Assert.Equal(StockDocumentStatus.PendingApproval, next);
    }

    [Fact]
    public void ApprovePendingStockDocument_moves_document_to_approved()
    {
        var service = new StockDocumentLifecycleService();

        var next = service.Approve(StockDocumentStatus.PendingApproval);

        Assert.Equal(StockDocumentStatus.Approved, next);
    }

    [Fact]
    public void EditPostedStockDocument_is_rejected()
    {
        var service = new StockDocumentLifecycleService();

        var ex = Assert.Throws<InventoryDomainException>(
            () => service.EnsureCanEditDetails(StockDocumentStatus.Posted));

        Assert.Equal("Posted documents cannot be edited directly.", ex.Message);
    }
}
