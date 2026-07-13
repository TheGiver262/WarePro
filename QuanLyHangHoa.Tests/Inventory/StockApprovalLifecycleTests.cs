using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public sealed class StockApprovalLifecycleTests
{
    [Fact]
    public void Posting_rejects_draft_document()
    {
        var service = new StockDocumentLifecycleService();

        var error = Assert.Throws<InventoryDomainException>(
            () => service.EnsureCanPost(StockDocumentStatus.Draft));

        Assert.Equal("Only approved documents can be posted.", error.Message);
    }

    [Fact]
    public void Posting_accepts_approved_document()
    {
        var service = new StockDocumentLifecycleService();

        service.EnsureCanPost(StockDocumentStatus.Approved);
    }

    [Fact]
    public void Approval_rejects_unauthorized_actor()
    {
        var service = new StockDocumentLifecycleService();

        var error = Assert.Throws<InventoryDomainException>(
            () => service.Approve(StockDocumentStatus.PendingApproval, isAuthorized: false));

        Assert.Equal("You are not authorized to approve stock documents.", error.Message);
    }
}
