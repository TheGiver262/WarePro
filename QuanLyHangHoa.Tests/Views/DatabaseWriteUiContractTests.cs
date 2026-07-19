using System.Xml.Linq;

namespace QuanLyHangHoa.Tests.Views;

public sealed class DatabaseWriteUiContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("StockIn")]
    [InlineData("StockOut")]
    [InlineData("StockTransfer")]
    [InlineData("StockAdjustment")]
    [InlineData("StockCount")]
    [InlineData("StockReversal")]
    public void Mutation_screen_exposes_write_state_and_status_line(string screen)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "ViewModels", $"{screen}ViewModel.cs"));
        var view = XDocument.Load(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Views", $"{screen}View.xaml"));

        Assert.Contains("[ObservableProperty] private bool _isWriting", source);
        Assert.Contains("[ObservableProperty] private string _writeStatus", source);
        Assert.Contains("DatabaseWriteUi.ExecuteAsync", source);
        Assert.Contains(view.Descendants(), element =>
            element.Name.LocalName == "TextBlock" &&
            ((string?)element.Attribute("Text"))?.Contains("{Binding WriteStatus", StringComparison.Ordinal) == true);
    }
}
