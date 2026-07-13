using System;
using System.IO;
using System.Xml.Linq;

namespace QuanLyHangHoa.Tests.Views;

public class StockAdjustmentSerialContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Serial_selector_preserves_layout_and_opens_multi_serial_dialog()
    {
        var document = XDocument.Load(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Views", "StockAdjustmentView.xaml"));

        var column = document.Descendants().Single(element =>
            element.Name.LocalName == "DataGridTemplateColumn" &&
            (string?)element.Attribute("Header") == "SERIAL");
        var selector = column.Descendants().Single(element => element.Name.LocalName == "ComboBox");

        Assert.Equal("180", (string?)column.Attribute("Width"));
        Assert.Equal("{StaticResource AppComboBoxStyle}", (string?)selector.Attribute("Style"));
        Assert.Equal("40", (string?)selector.Attribute("Height"));
        Assert.Equal("4", (string?)selector.Attribute("Margin"));
        Assert.Equal(
            "{Binding IsSerialTracked, Converter={StaticResource BooleanToVisibilityConverter}}",
            (string?)selector.Attribute("Visibility"));
        Assert.Equal(
            "{Binding DataContext.IsEditMode, RelativeSource={RelativeSource AncestorType=UserControl}}",
            (string?)selector.Attribute("IsEnabled"));
        Assert.Equal("SerialSelector_PreviewMouseLeftButtonDown",
            (string?)selector.Attribute("PreviewMouseLeftButtonDown"));
        Assert.Equal("{Binding SerialDisplay, Mode=OneWay}", (string?)selector.Attribute("Text"));
    }
}
