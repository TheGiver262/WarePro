using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class MainViewModelCommandTests
{
    [Fact]
    public void WarrantyCoverage_navigation_command_is_generated()
    {
        Assert.NotNull(typeof(MainViewModel).GetProperty("OpenWarrantyCoverageViewCommand"));
    }

    [Theory]
    [InlineData("CanAccessStockIn")]
    [InlineData("CanAccessStockOut")]
    [InlineData("CanAccessStockAdjustment")]
    [InlineData("CanAccessPurchaseInvoices")]
    [InlineData("CanAccessSalesInvoices")]
    [InlineData("CanAccessWarranty")]
    [InlineData("CanAccessReports")]
    public void Navigation_permission_is_exposed_for_sidebar_binding(string propertyName)
    {
        Assert.NotNull(typeof(MainViewModel).GetProperty(propertyName));
    }
}
