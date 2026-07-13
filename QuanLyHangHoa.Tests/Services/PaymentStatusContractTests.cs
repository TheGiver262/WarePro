using System;
using System.Linq;

using System.IO;
using System.Reflection;
using System.Xml.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class PaymentStatusContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Current_schema_normalizes_payment_status_constraints()
    {
        var type = typeof(DatabaseInitializer);
        var versionField = type.GetField("CurrentSchemaVersion", BindingFlags.NonPublic | BindingFlags.Static);
        var sqlField = type.GetField("SchemaVersion5Sql", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(versionField);
        Assert.NotNull(sqlField);
        Assert.Equal(5, (int)versionField.GetRawConstantValue()!);
        var sql = (string)sqlField.GetRawConstantValue()!;
        Assert.Contains("UPPER(PaymentStatus)", sql);
        Assert.Contains("SET PaymentStatus = 'PartiallyPaid'", sql);
        Assert.Contains("CK_SalesInvoice_PaymentStatus", sql);
        Assert.Contains("CK_PurchaseInvoice_PaymentStatus", sql);
        Assert.Contains("'Unpaid', 'PartiallyPaid', 'Paid', 'Overdue'", sql);
    }

    [Theory]
    [InlineData("unpaid", PaymentStatus.Unpaid)]
    [InlineData("PARTIAL", PaymentStatus.PartiallyPaid)]
    [InlineData("partiallypaid", PaymentStatus.PartiallyPaid)]
    [InlineData("pAiD", PaymentStatus.Paid)]
    [InlineData("OVERDUE", PaymentStatus.Overdue)]
    public void Normalize_returns_canonical_casing(string input, string expected)
    {
        Assert.Equal(expected, PaymentStatus.Normalize(input));
    }

    [Fact]
    public void Invoice_print_maps_canonical_partial_payment_status()
    {
        var invoice = new SalesInvoice
        {
            InvoiceCode = "HDB-PARTIAL",
            InvoiceDate = new DateTime(2026, 7, 12),
            Customer = new Customer { DisplayName = "Customer", CustomerCode = "KH" },
            PaymentStatus = PaymentStatus.PartiallyPaid
        };

        var model = DocumentPrintModel.FromSalesInvoice(invoice);

        Assert.Equal("Thanh toán một phần", model.StatusText);
    }

    [Fact]
    public void Payment_badge_keeps_styles_and_uses_canonical_partial_status()
    {
        var path = Path.Combine(RepoRoot, "QuanLyHangHoa", "Themes", "Tables.xaml");
        var document = XDocument.Load(path);
        var paymentTemplate = document.Descendants()
            .Single(element => (string?)element.Attribute(XName.Get(
                "Key",
                "http://schemas.microsoft.com/winfx/2006/xaml")) == "PaymentStatusBadgeTemplate");
        var triggers = paymentTemplate.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger"
                && ((string?)element.Attribute("Binding"))?.Contains(
                    "PaymentStatus",
                    StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(8, triggers.Count);
        Assert.Equal(2, triggers.Count(trigger => (string?)trigger.Attribute("Value")
            == "{x:Static models:PaymentStatus.PartiallyPaid}"));
        Assert.DoesNotContain(triggers, trigger => (string?)trigger.Attribute("Value") == "Partial");

        var partialTriggers = triggers.Where(trigger => (string?)trigger.Attribute("Value")
            == "{x:Static models:PaymentStatus.PartiallyPaid}").ToList();
        Assert.Contains(partialTriggers[0].Descendants(), setter =>
            (string?)setter.Attribute("Value") == "{StaticResource WarningBgBrush}");
        Assert.Contains(partialTriggers[1].Descendants(), setter =>
            (string?)setter.Attribute("Value") == "{StaticResource WarningTextBrush}");
    }
}
