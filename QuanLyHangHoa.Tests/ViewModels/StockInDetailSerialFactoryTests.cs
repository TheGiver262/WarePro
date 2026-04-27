using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels;

public class StockInDetailSerialFactoryTests
{
    [Fact]
    public void CreateSerials_without_user_input_returns_no_serials()
    {
        var product = new Product { Id = 10, IsSerialManaged = false };

        var serials = StockInDetailSerialFactory.CreateSerials(product, string.Empty);

        Assert.Empty(serials);
    }

    [Fact]
    public void CreateSerials_with_user_input_returns_entered_serials()
    {
        var product = new Product { Id = 20, IsSerialManaged = true };

        var serials = StockInDetailSerialFactory.CreateSerials(product, "SN-001, SN-002");

        Assert.Equal(new[] { "SN-001", "SN-002" }, serials.Select(s => s.SerialNumber));
        Assert.All(serials, serial =>
        {
            Assert.Equal(20, serial.ProductId);
            Assert.Equal("InStock", serial.Status);
        });
    }
}
