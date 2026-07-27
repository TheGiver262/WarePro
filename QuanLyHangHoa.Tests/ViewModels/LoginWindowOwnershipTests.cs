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

    [Fact]
    public void Logout_promotes_login_window_before_closing_main_window()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var source = File.ReadAllText(Path.Combine(
            root, "QuanLyHangHoa", "ViewModels", "MainViewModel.cs"));
        var logoutStart = source.IndexOf("private void Logout()", StringComparison.Ordinal);
        Assert.True(logoutStart >= 0, "missing Logout command");

        var logout = source[logoutStart..];
        var create = logout.IndexOf("var login = new LoginView();", StringComparison.Ordinal);
        var promote = logout.IndexOf("Application.Current.MainWindow = login;", StringComparison.Ordinal);
        var show = logout.IndexOf("login.Show();", StringComparison.Ordinal);
        var close = logout.IndexOf("window.Close();", StringComparison.Ordinal);

        Assert.True(create >= 0, "logout must create a LoginView instance");
        Assert.True(promote > create, "logout must promote LoginView after creating it");
        Assert.True(show > promote, "logout must show LoginView after promoting it");
        Assert.True(close > show, "MainWindow must close after LoginView is visible");
    }
}