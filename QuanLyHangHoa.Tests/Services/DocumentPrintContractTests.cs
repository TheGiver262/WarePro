using System.IO;
using System.Xml.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class DocumentPrintContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Purchase_invoice_maps_partner_tax_and_totals()
    {
        var invoice = new PurchaseInvoice
        {
            InvoiceCode = "HDN-001",
            InvoiceDate = new DateTime(2026, 7, 12),
            Supplier = new Supplier { DisplayName = "Nhà cung cấp A", SupplierCode = "NCC-A" },
            SubTotal = 100_000,
            TaxAmount = 10_000,
            GrandTotal = 110_000,
            PaidAmount = 60_000,
            Lines =
            {
                new PurchaseInvoiceLine
                {
                    Product = Product("SP01", "Sản phẩm A"),
                    Unit = Unit(),
                    Quantity = 2,
                    UnitPrice = 50_000,
                    TaxRate = 0.1m,
                    GrandTotal = 110_000
                }
            }
        };

        var model = DocumentPrintModel.FromPurchaseInvoice(invoice);

        Assert.Equal("HÓA ĐƠN MUA HÀNG", model.Title);
        Assert.Equal("Nhà cung cấp A", model.PartnerName);
        Assert.Equal(50_000, model.RemainingAmount);
        Assert.Single(model.Lines);
        Assert.True(model.ShowPaymentSummary);
    }

    [Fact]
    public void Sales_invoice_maps_customer()
    {
        var invoice = new SalesInvoice
        {
            InvoiceCode = "HDB-001",
            InvoiceDate = new DateTime(2026, 7, 12),
            Customer = new Customer { DisplayName = "Khách hàng A", CustomerCode = "KH-A" },
            Lines =
            {
                new SalesInvoiceLine
                {
                    Product = Product("SP01", "Sản phẩm A"),
                    Unit = Unit(),
                    Quantity = 1,
                    UnitPrice = 25_000,
                    GrandTotal = 25_000
                }
            }
        };

        var model = DocumentPrintModel.FromSalesInvoice(invoice);

        Assert.Equal("HÓA ĐƠN BÁN HÀNG", model.Title);
        Assert.Equal("Khách hàng A", model.PartnerName);
        Assert.Single(model.Lines);
    }

    [Fact]
    public void Stock_documents_map_warehouse_and_calculated_total()
    {
        var stockIn = new StockIn
        {
            DocumentCode = "PN-001",
            Warehouse = new Warehouse { DisplayName = "Kho chính", WarehouseCode = "KHO" },
            Supplier = new Supplier { DisplayName = "Nhà cung cấp A", SupplierCode = "NCC-A" },
            Lines =
            {
                new StockInLine
                {
                    Product = Product("SP01", "Sản phẩm A"),
                    Unit = Unit(),
                    Quantity = 3,
                    UnitPrice = 20_000
                }
            }
        };
        var stockOut = new StockOut
        {
            DocumentCode = "PX-001",
            Warehouse = new Warehouse { DisplayName = "Kho chính", WarehouseCode = "KHO" },
            Customer = new Customer { DisplayName = "Khách hàng A", CustomerCode = "KH-A" },
            Lines =
            {
                new StockOutLine
                {
                    Product = Product("SP01", "Sản phẩm A"),
                    Unit = Unit(),
                    Quantity = 2,
                    UnitPrice = 25_000
                }
            }
        };

        var stockInModel = DocumentPrintModel.FromStockIn(stockIn);
        var stockOutModel = DocumentPrintModel.FromStockOut(stockOut);

        Assert.Equal("PHIẾU NHẬP KHO", stockInModel.Title);
        Assert.Equal(60_000, stockInModel.GrandTotal);
        Assert.Equal("PHIẾU XUẤT KHO", stockOutModel.Title);
        Assert.Equal(50_000, stockOutModel.GrandTotal);
        Assert.False(stockOutModel.ShowPaymentSummary);
    }

    [Fact]
    public void Print_preview_and_commands_have_no_development_placeholders()
    {
        var xamlPath = Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", "DocumentPrintWindow.xaml");
        Assert.True(File.Exists(xamlPath));
        var xaml = File.ReadAllText(xamlPath);
        Assert.Contains("x:Name=\"PrintArea\"", xaml);
        Assert.Contains("WindowStartupLocation=\"CenterOwner\"", xaml);
        Assert.Contains("PrintButton_Click", xaml);
        Assert.Contains("Command=\"{Binding CreateFromStockInCommand}\"",
            File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", "PurchaseInvoiceView.xaml")));
        Assert.Contains("Command=\"{Binding CreateFromStockOutCommand}\"",
            File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", "SalesInvoiceView.xaml")));

        foreach (var file in new[]
        {
            "PurchaseInvoiceViewModel.cs",
            "SalesInvoiceViewModel.cs",
            "StockInViewModel.cs",
            "StockOutViewModel.cs"
        })
        {
            var source = File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "ViewModels", file));
            Assert.DoesNotContain("đang phát triển", source, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DocumentPrintWindow", source);
        }
    }

    [Fact]
    public void Text_action_buttons_are_wired()
    {
        var viewsDirectory = Path.Combine(RepoRoot, "QuanLyHangHoa", "Views");
        var unwired = Directory.EnumerateFiles(viewsDirectory, "*.xaml")
            .SelectMany(path => XDocument.Load(path).Descendants()
                .Where(element => element.Name.LocalName == "Button"
                    && element.Attribute("Content") != null
                    && element.Attribute("Command") == null
                    && element.Attribute("Click") == null
                    && element.Attribute("IsCancel") == null)
                .Select(element => $"{Path.GetFileName(path)}: {(string?)element.Attribute("Content")}"))
            .ToList();

        Assert.Empty(unwired);
    }

    private static Product Product(string code, string name) => new()
    {
        ProductCode = code,
        DisplayName = name
    };

    private static Unit Unit() => new()
    {
        UnitCode = "CAI",
        DisplayName = "Cái"
    };
}
