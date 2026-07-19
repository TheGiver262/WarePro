using System.Reflection;

namespace QuanLyHangHoa.Tests.ViewModels;

public sealed class LoginWindowOwnershipTests
{
    [Fact]
    public void Successful_login_promotes_main_window_before_closing_login_window()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(
            root, "QuanLyHangHoa", "ViewModels", "LoginViewModel.cs"));

        var promote = source.IndexOf("Application.Current.MainWindow = main;", StringComparison.Ordinal);
        var close = source.IndexOf("currentWindow?.Close();", StringComparison.Ordinal);

        Assert.True(promote >= 0, "successful login must promote MainWindow");
        Assert.True(close > promote, "login window must close after MainWindow is promoted");
    }
}