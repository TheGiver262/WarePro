namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class AuditArchiveViewModelContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Archive_retry_reuses_operation_and_file_until_success()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot,
            "QuanLyHangHoa",
            "ViewModels",
            "AuditLogViewModel.cs"));

        Assert.Contains("private Guid? _archiveOperationId;", source, StringComparison.Ordinal);
        Assert.Contains("private string? _archiveFilePath;", source, StringComparison.Ordinal);
        Assert.Contains("if (_archiveOperationId is null)", source, StringComparison.Ordinal);
        Assert.Contains("_archiveOperationId = Guid.NewGuid();", source, StringComparison.Ordinal);
        Assert.Contains("_archiveFilePath = saveFileDialog.FileName;", source, StringComparison.Ordinal);
        Assert.Contains("_archiveOperationId.Value", source, StringComparison.Ordinal);
        Assert.Contains("ResetArchiveOperation();", source, StringComparison.Ordinal);
    }
}