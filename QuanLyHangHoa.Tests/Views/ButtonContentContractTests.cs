using System.IO;
using System.Xml.Linq;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.Views;

public class ButtonContentContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Refresh_and_excel_buttons_define_their_own_content()
    {
        var viewsDirectory = Path.Combine(RepoRoot, "QuanLyHangHoa", "Views");
        var missing = Directory.GetFiles(viewsDirectory, "*.xaml")
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .Where(element => IsSharedContentStyle((string?)element.Attribute("Style")))
                .Where(element => element.Attribute("Content") is null && !element.Elements().Any())
                .Select(_ => Path.GetFileName(file)))
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Shared_button_styles_do_not_reuse_visual_content()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "QuanLyHangHoa", "Themes", "Buttons.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var offenders = document.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Where(element => (string?)element.Attribute(x + "Key") is "AppRefreshButton" or "AppExcelButton")
            .SelectMany(style => style.Elements())
            .Where(element => element.Name.LocalName == "Setter" && (string?)element.Attribute("Property") == "Content")
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsSharedContentStyle(string? style) =>
        style is "{StaticResource AppRefreshButton}" or "{StaticResource AppExcelButton}";

    [Theory]
    [InlineData(typeof(PurchaseInvoiceViewModel))]
    [InlineData(typeof(SalesInvoiceViewModel))]
    public void Invoice_excel_button_has_a_command(Type viewModelType)
    {
        Assert.NotNull(viewModelType.GetProperty("ExportToExcelCommand"));
    }
}
