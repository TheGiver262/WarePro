using System;
using System.IO;
using System.Xml.Linq;

namespace QuanLyHangHoa.Tests.Views;

public sealed class StockLifecycleUiContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("StockInView.xaml")]
    [InlineData("StockOutView.xaml")]
    public void List_approval_action_supports_pending_and_approved_without_new_controls(string fileName)
    {
        var document = LoadView(fileName);
        var button = FindCommandButton(document, "ApproveDocumentCommand");
        var statuses = button.Descendants()
            .Where(element => element.Name.LocalName == "Condition" &&
                (string?)element.Attribute("Binding") == "{Binding Status}")
            .Select(element => (string?)element.Attribute("Value"))
            .ToList();

        Assert.Contains("PendingApproval", statuses);
        Assert.Contains("Approved", statuses);
        Assert.Equal("{StaticResource ProMaxIconButtonStyle}",
            (string?)button.Descendants().Single(element => element.Name.LocalName == "Style").Attribute("BasedOn"));
    }

    [Theory]
    [InlineData("StockInView.xaml", "Visibility", "{Binding CanApprove, Converter={StaticResource BooleanToVisibilityConverter}}", "36")]
    [InlineData("StockOutView.xaml", "Visibility", "{Binding CanApprove, Converter={StaticResource BooleanToVisibilityConverter}}", "36")]
    [InlineData("StockTransferView.xaml", "Visibility", "{Binding CanProcessLifecycle, Converter={StaticResource BooleanToVisibilityConverter}}", "36")]
    [InlineData("StockAdjustmentView.xaml", "IsEnabled", "{Binding CanProcessLifecycle}", null)]
    public void Detail_lifecycle_action_preserves_existing_primary_button(
        string fileName,
        string stateAttribute,
        string expectedBinding,
        string? expectedHeight)
    {
        var document = LoadView(fileName);
        var button = FindCommandButton(document, "ConfirmAndPostCommand");

        Assert.Equal("{StaticResource PrimaryButton}", (string?)button.Attribute("Style"));
        Assert.Equal(expectedBinding, (string?)button.Attribute(stateAttribute));
        Assert.Equal(expectedHeight, (string?)button.Attribute("Height"));
    }

    private static XElement FindCommandButton(XDocument document, string commandName) =>
        document.Descendants().Single(element =>
            element.Name.LocalName == "Button" &&
            ((string?)element.Attribute("Command"))?.Contains(commandName, StringComparison.Ordinal) == true);

    private static XDocument LoadView(string fileName) =>
        XDocument.Load(Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", fileName));
}
