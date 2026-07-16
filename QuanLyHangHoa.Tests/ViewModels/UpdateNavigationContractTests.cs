using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class UpdateNavigationContractTests
{
    [Fact]
    public void Main_window_exposes_update_menu_and_available_badge()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(root, "QuanLyHangHoa", "MainWindow.xaml"));

        Assert.Contains("Cập nhật WarePro", xaml, StringComparison.Ordinal);
        Assert.Contains("OpenUpdateViewCommand", xaml, StringComparison.Ordinal);
        Assert.Contains("HasUpdateAvailable", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("WebBrowser", xaml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Main_view_model_generates_the_update_navigation_command()
    {
        Assert.NotNull(typeof(MainViewModel).GetProperty("OpenUpdateViewCommand"));
        Assert.NotNull(typeof(MainViewModel).GetProperty("HasUpdateAvailable"));
        Assert.NotNull(typeof(MainViewModel).GetMethod("CheckForUpdatesAutomaticallyAsync"));
    }
}
