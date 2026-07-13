using System.Linq;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.ViewModels;

public class StockInSerialParsingTests
{
    [Fact]
    public void ParseSerialRange_empty_input_returns_empty_list()
    {
        var serials = StockInService.ParseSerialRange(string.Empty);
        Assert.Empty(serials);
    }

    [Fact]
    public void ParseSerialRange_comma_separated_returns_list()
    {
        var serials = StockInService.ParseSerialRange("SN-001, SN-002");
        Assert.Equal(new[] { "SN-001", "SN-002" }, serials);
    }

    [Fact]
    public void ParseSerialRange_supports_numeric_ranges()
    {
        var serials = StockInService.ParseSerialRange("SN-001-003");
        Assert.Equal(new[] { "SN-001", "SN-002", "SN-003" }, serials);
    }

    [Fact]
    public void ParseSerialRange_supports_mixed_input()
    {
        var serials = StockInService.ParseSerialRange("SN-001-002, MANUAL-01");
        Assert.Equal(new[] { "SN-001", "SN-002", "MANUAL-01" }, serials);
    }

    [Theory]
    [InlineData("SN-003-001")]
    [InlineData("ABC123-001")]
    public void ParseSerialRange_preserves_range_like_serial_when_range_is_not_ascending(string input)
    {
        var serials = StockInService.ParseSerialRange(input);

        Assert.Equal(new[] { input }, serials);
    }
}
