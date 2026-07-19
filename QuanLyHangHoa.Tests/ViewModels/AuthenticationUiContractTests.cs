using System.IO;

namespace QuanLyHangHoa.Tests.ViewModels;

public class AuthenticationUiContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Login_command_handles_write_conflict_and_requests_retry()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "ViewModels", "LoginViewModel.cs"));
        var start = source.IndexOf("private async Task Login", StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing async Login command.");
        var method = source[start..];

        Assert.Contains("catch (DatabaseWriteConflictException)", method);
        Assert.Contains("thử đăng nhập lại", method, StringComparison.OrdinalIgnoreCase);
    }
}
