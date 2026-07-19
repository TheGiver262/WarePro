using System;
using System.IO;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class StockOutTransferWriteSafetyTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    [Theory]
    [InlineData("StockOutService.cs", "SaveDraftSnapshot", "BuildStockOut")]
    [InlineData("StockTransferService.cs", "SaveDraftSnapshot", "BuildStockTransfer")]
    public void SaveDraft_uses_immutable_snapshot_and_fresh_graph_per_attempt(
        string fileName,
        string snapshotType,
        string graphFactory)
    {
        var source = ReadService(fileName);

        Assert.Contains(snapshotType, source, StringComparison.Ordinal);
        Assert.Contains(graphFactory, source, StringComparison.Ordinal);
        Assert.DoesNotContain("item.DocumentCode == documentCode && item.Status == DocumentStatus.Draft", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("StockOutService.cs", "stock-out.delete", "db.StockOutLines.RemoveRange")]
    [InlineData("StockTransferService.cs", "stock-transfer.delete", "db.StockTransferLines.RemoveRange")]
    public void Delete_uses_executor_rowversion_and_one_staged_unit_of_work(
        string fileName,
        string operationName,
        string lineRemoval)
    {
        var source = ReadService(fileName);

        Assert.Contains("public Task DeleteAsync(", source, StringComparison.Ordinal);
        Assert.Contains(operationName, source, StringComparison.Ordinal);
        Assert.Contains("OriginalValue = expectedRowVersion", source, StringComparison.Ordinal);
        Assert.Contains(lineRemoval, source, StringComparison.Ordinal);
        Assert.DoesNotContain("public virtual void Delete(", source, StringComparison.Ordinal);
    }

    private static string ReadService(string fileName) =>
        File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "Services", fileName));
}
