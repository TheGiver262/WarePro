namespace QuanLyHangHoa.Tests.Deployment;

public class ReleaseRunbookContractTests
{
    [Fact]
    public void Release_runbook_covers_build_smoke_promotion_and_recovery_gates()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var runbook = File.ReadAllText(Path.Combine(
            root, "docs", "operations", "WAREPRO_RELEASE_RUNBOOK.md"));

        foreach (var requiredText in new[]
                 {
                     "cổng phát hành",
                     "ma trận máy sạch",
                     "security drill",
                     "shared database",
                     "rollback",
                     "Build-WareProRelease.ps1",
                     "Verify-WareProRelease.ps1",
                     "Invoke-WareProInstallerSmoke.ps1",
                     "evidence-",
                     "ba installer stable",
                     "không dùng production"
                 })
        {
            Assert.Contains(requiredText, runbook, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Full", runbook, StringComparison.Ordinal);
        Assert.Contains("AppOnly", runbook, StringComparison.Ordinal);
        Assert.Contains("Upgrade", runbook, StringComparison.Ordinal);
        Assert.Contains("Uninstall", runbook, StringComparison.Ordinal);
    }
}
