using System;
using System.IO;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class StockDocumentConcurrencyContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("StockInService.cs", "StockIns")]
    [InlineData("StockOutService.cs", "StockOuts")]
    [InlineData("StockTransferService.cs", "StockTransfers")]
    [InlineData("StockAdjustmentService.cs", "StockAdjustments")]
    public void Services_reject_missing_updates_and_refresh_committed_rowversion(
        string fileName,
        string dbSet)
    {
        var source = Read("Services", fileName);

        Assert.Contains("snapshot.Id > 0 && existing is null", source, StringComparison.Ordinal);
        Assert.Contains("chứng từ đã bị xóa", source, StringComparison.Ordinal);
        Assert.Contains($"db.{dbSet}.AsNoTracking()", source, StringComparison.Ordinal);
        Assert.Contains("LoadRowVersionAsync", source, StringComparison.Ordinal);
        Assert.Contains("OriginalValue = expectedRowVersion", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StockInService.cs")]
    [InlineData("StockOutService.cs")]
    [InlineData("StockTransferService.cs")]
    [InlineData("StockAdjustmentService.cs")]
    public void Lifecycle_overloads_accept_and_return_rowversion(string fileName)
    {
        var source = Read("Services", fileName);

        Assert.Contains("Task<byte[]> SubmitForApprovalAsync(", source, StringComparison.Ordinal);
        Assert.Contains("Task<byte[]> ApproveAsync(", source, StringComparison.Ordinal);
        Assert.Contains("Task<byte[]> PostAsync(", source, StringComparison.Ordinal);
        Assert.Contains("byte[] expectedRowVersion", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StockInViewModel.cs", "_stockInService")]
    [InlineData("StockOutViewModel.cs", "_stockOutService")]
    [InlineData("StockTransferViewModel.cs", "_stockTransferService")]
    [InlineData("StockAdjustmentViewModel.cs", "_adjustmentService")]
    public void ViewModels_carry_latest_rowversion_through_mutations(
        string fileName,
        string serviceField)
    {
        var source = Read("ViewModels", fileName);

        Assert.Contains("private byte[] _editingRowVersion = [];", source, StringComparison.Ordinal);
        Assert.Contains("RowVersion = _editingRowVersion.ToArray()", source, StringComparison.Ordinal);
        Assert.Contains($"await {serviceField}.SubmitForApprovalAsync(", source, StringComparison.Ordinal);
        Assert.Contains($"await {serviceField}.ApproveAsync(", source, StringComparison.Ordinal);
        Assert.Contains($"await {serviceField}.PostAsync(", source, StringComparison.Ordinal);
        Assert.Contains("_editingRowVersion = await", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StockInViewModel.cs")]
    [InlineData("StockOutViewModel.cs")]
    public void Stock_in_and_out_draft_loaders_prefer_draft_serials(string fileName)
    {
        var source = Read("ViewModels", fileName);

        Assert.Contains("StockDocumentUiLifecycle.IsDraft", source, StringComparison.Ordinal);
        Assert.Contains("line.DraftSerials.Split", source, StringComparison.Ordinal);
    }

    private static string Read(string folder, string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", folder, fileName));
    [Theory]
    [InlineData("StockIn", "_stockInService")]
    [InlineData("StockOut", "_stockOutService")]
    [InlineData("StockTransfer", "_stockTransferService")]
    [InlineData("StockAdjustment", "_adjustmentService")]
    public void Draft_delete_commands_pass_rowversion_and_are_bound_in_views(
        string documentName,
        string serviceField)
    {
        var viewModel = Read("ViewModels", $"{documentName}ViewModel.cs");
        var view = Read("Views", $"{documentName}View.xaml");

        Assert.Contains("private async Task DeleteDocument(", viewModel, StringComparison.Ordinal);
        Assert.Contains("StockDocumentUiLifecycle.IsDraft(document.Status)", viewModel, StringComparison.Ordinal);
        Assert.Contains($"await {serviceField}.DeleteAsync(document.Id, document.RowVersion,", viewModel, StringComparison.Ordinal);
        Assert.Contains("DeleteDocumentCommand", view, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"{Binding}\"", view, StringComparison.Ordinal);
    }

}
