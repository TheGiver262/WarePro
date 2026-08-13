using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.ViewModels;
using System.IO;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class StockDocumentLifecycleTests
{
    [Theory]
    [InlineData(DocumentStatus.Draft, DocumentStatus.Draft, "Phiếu nháp")]
    [InlineData("nháp", DocumentStatus.Draft, "Phiếu nháp")]
    [InlineData(DocumentStatus.PendingApproval, DocumentStatus.PendingApproval, "Chờ duyệt")]
    [InlineData("chờ duyệt", DocumentStatus.PendingApproval, "Chờ duyệt")]
    [InlineData(DocumentStatus.Approved, DocumentStatus.Approved, "Đã duyệt")]
    [InlineData("đã duyệt", DocumentStatus.Approved, "Đã duyệt")]
    [InlineData(DocumentStatus.Posted, DocumentStatus.Posted, "Đã ghi sổ")]
    [InlineData("đã ghi sổ", DocumentStatus.Posted, "Đã ghi sổ")]
    public void Ui_lifecycle_maps_canonical_and_legacy_statuses(
        string input,
        string canonical,
        string displayLabel)
    {
        Assert.Equal(canonical, StockDocumentUiLifecycle.ParseFilter(input));
        Assert.Equal(displayLabel, StockDocumentUiLifecycle.GetDisplayLabel(input));
    }

    [Fact]
    public void Active_document_exports_use_the_shared_lifecycle_label()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        foreach (var file in new[]
                 {
                     "StockInViewModel.cs",
                     "StockOutViewModel.cs",
                     "StockAdjustmentViewModel.cs"
                 })
        {
            var source = File.ReadAllText(Path.Combine(repoRoot, "QuanLyHangHoa", "ViewModels", file));
            Assert.Contains("StockDocumentUiLifecycle.GetDisplayLabel", source);
        }
    }

    [Fact]
    public void SubmitDraftStockDocument_moves_document_to_pending_approval()
    {
        var service = new StockDocumentLifecycleService();

        var next = service.SubmitForApproval(StockDocumentStatus.Draft);

        Assert.Equal(StockDocumentStatus.PendingApproval, next);
    }

    [Theory]
    [InlineData(StockDocumentStatus.PendingApproval)]
    [InlineData(StockDocumentStatus.Approved)]
    [InlineData(StockDocumentStatus.Posted)]
    [InlineData(StockDocumentStatus.Locked)]
    [InlineData(StockDocumentStatus.Cancelled)]
    public void SubmitForApproval_rejects_non_draft_documents(StockDocumentStatus current)
    {
        var service = new StockDocumentLifecycleService();

        var ex = Assert.Throws<InventoryDomainException>(
            () => service.SubmitForApproval(current));

        Assert.Equal("Only draft documents can be submitted for approval.", ex.Message);
    }

    [Fact]
    public void ApprovePendingStockDocument_moves_document_to_approved()
    {
        var service = new StockDocumentLifecycleService();

        var next = service.Approve(StockDocumentStatus.PendingApproval);

        Assert.Equal(StockDocumentStatus.Approved, next);
    }

    [Theory]
    [InlineData(StockDocumentStatus.Draft)]
    [InlineData(StockDocumentStatus.Approved)]
    [InlineData(StockDocumentStatus.Posted)]
    [InlineData(StockDocumentStatus.Locked)]
    [InlineData(StockDocumentStatus.Cancelled)]
    public void Approve_rejects_non_pending_approval_documents(StockDocumentStatus current)
    {
        var service = new StockDocumentLifecycleService();

        var ex = Assert.Throws<InventoryDomainException>(
            () => service.Approve(current));

        Assert.Equal("Only pending documents can be approved.", ex.Message);
    }

    [Fact]
    public void EditPostedStockDocument_is_rejected()
    {
        var service = new StockDocumentLifecycleService();

        var ex = Assert.Throws<InventoryDomainException>(
            () => service.EnsureCanEditDetails(StockDocumentStatus.Posted));

        Assert.Equal("Posted documents cannot be edited directly.", ex.Message);
    }

    [Fact]
    public void EditLockedStockDocument_is_rejected()
    {
        var service = new StockDocumentLifecycleService();

        var ex = Assert.Throws<InventoryDomainException>(
            () => service.EnsureCanEditDetails(StockDocumentStatus.Locked));

        Assert.Equal("Locked documents cannot be edited.", ex.Message);
    }
}
