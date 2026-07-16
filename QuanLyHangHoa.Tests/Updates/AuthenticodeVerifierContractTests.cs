using System.IO;

namespace QuanLyHangHoa.Tests.Updates;

public class AuthenticodeVerifierContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Verifier_reads_a_real_timestamp_countersigner_and_closes_WinTrust_state()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Updates", "AuthenticodeVerifier.cs"));

        Assert.Contains("StateActionVerify", source, StringComparison.Ordinal);
        Assert.Contains("StateActionClose", source, StringComparison.Ordinal);
        Assert.Contains("WTHelperProvDataFromStateData", source, StringComparison.Ordinal);
        Assert.Contains("WTHelperGetProvSignerFromChain", source, StringComparison.Ordinal);
        Assert.Contains("CounterSignerCount", source, StringComparison.Ordinal);
        Assert.Contains(
            "TimestampValid: trusted && HasTrustedTimestamp(trustData.StateData)",
            source,
            StringComparison.Ordinal);
    }
}
