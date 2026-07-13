using System;
using System.IO;

namespace QuanLyHangHoa.Tests.Views;

public class SerialInputWindowContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Confirm_requires_at_least_one_parsed_serial_before_closing()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Views", "SerialInputWindow.xaml.cs"));
        var methodStart = source.IndexOf("private void Confirm_Click", StringComparison.Ordinal);
        var methodEnd = source.IndexOf("private void Cancel_Click", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0);
        Assert.True(methodEnd > methodStart);

        var method = source[methodStart..methodEnd];
        var parse = method.IndexOf("StockInService.ParseSerialRange(SerialTextBox.Text)", StringComparison.Ordinal);
        var emptyGuard = method.IndexOf("if (_requireNonEmptySerials && serials.Count == 0)", StringComparison.Ordinal);
        var earlyReturn = method.IndexOf("return;", StringComparison.Ordinal);
        var assignment = method.IndexOf("SerialInput = SerialTextBox.Text;", StringComparison.Ordinal);
        var confirm = method.IndexOf("DialogResult = true;", StringComparison.Ordinal);

        Assert.True(parse >= 0);
        Assert.True(emptyGuard > parse);
        Assert.True(earlyReturn > emptyGuard);
        Assert.True(assignment > earlyReturn);
        Assert.True(confirm > assignment);
    }

    [Theory]
    [InlineData("StockInViewModel.cs")]
    [InlineData("StockOutViewModel.cs")]
    [InlineData("StockTransferViewModel.cs")]
    public void Quantity_based_documents_require_non_empty_serials(string fileName)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "ViewModels", fileName));

        Assert.Contains("requireNonEmptySerials: true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Stock_count_keeps_existing_empty_serial_behavior()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "ViewModels", "StockCountViewModel.cs"));

        Assert.DoesNotContain("requireNonEmptySerials: true", source, StringComparison.Ordinal);
    }
}
