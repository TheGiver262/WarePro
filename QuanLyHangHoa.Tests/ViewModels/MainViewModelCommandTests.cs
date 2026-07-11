using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class MainViewModelCommandTests
{
    [Fact]
    public void WarrantyCoverage_navigation_command_is_generated()
    {
        Assert.NotNull(typeof(MainViewModel).GetProperty("OpenWarrantyCoverageViewCommand"));
    }
}
