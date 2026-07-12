using System.IO;
using System.Reflection;
using System.Xml.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.Views;

public class UiRegressionContractTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("PurchaseInvoiceView.xaml", "Supplier.DisplayName", "{DynamicResource AppDataGridText}")]
    [InlineData("SalesInvoiceView.xaml", "Customer.DisplayName", "{DynamicResource AppDataGridText}")]
    [InlineData("WarrantyView.xaml", "CustomerName", "{DynamicResource AppDataGridText}")]
    [InlineData("WarrantyView.xaml", "ReceivedDate", "{DynamicResource AppDataGridTextCenter}")]
    [InlineData("WarrantyView.xaml", "ExpectedReturnDate", "{DynamicResource AppDataGridTextCenter}")]
    public void Data_columns_use_consistent_cell_alignment(string fileName, string bindingText, string expectedStyle)
    {
        var document = LoadView(fileName);
        var column = document.Descendants()
            .Single(element => element.Name.LocalName == "DataGridTextColumn"
                && ((string?)element.Attribute("Binding"))?.Contains(bindingText, StringComparison.Ordinal) == true);

        Assert.Equal(expectedStyle, (string?)column.Attribute("ElementStyle"));
    }

    [Fact]
    public void Warranty_serial_uses_standard_data_typography()
    {
        var document = LoadView("WarrantyCoverageView.xaml");
        var serial = document.Descendants()
            .Single(element => element.Name.LocalName == "TextBlock"
                && ((string?)element.Attribute("Text"))?.Contains("ProductSerial.SerialNumber", StringComparison.Ordinal) == true);

        Assert.Null(serial.Attribute("Style"));
        Assert.Null(serial.Attribute("FontWeight"));
    }

    [Fact]
    public void Warranty_navigation_and_header_use_the_same_name()
    {
        Assert.Contains("Content=\"Quản lý bảo hành\"", File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "MainWindow.xaml")));
        Assert.Contains("Text=\"QUẢN LÝ BẢO HÀNH\"", File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", "WarrantyView.xaml")));
    }

    [Fact]
    public void Report_uses_short_warehouse_log_tab_name()
    {
        var document = LoadView("ReportView.xaml");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "TabItem" && (string?)element.Attribute("Header") == "NHẬT KÝ KHO");
    }

    [Fact]
    public void Serial_trace_summary_has_six_columns_and_detail_action()
    {
        var document = LoadView("ReportView.xaml");
        var tab = document.Descendants().Single(element =>
            element.Name.LocalName == "TabItem" && (string?)element.Attribute("Header") == "TRUY VẾT LỊCH SỬ SERIAL");
        var columns = tab.Descendants().Single(element => element.Name.LocalName == "DataGrid.Columns");

        Assert.Equal(6, columns.Elements().Count());
        Assert.Contains("ViewSerialTraceDetailCommand", columns.ToString());
        Assert.NotNull(typeof(ReportViewModel).GetProperty("ViewSerialTraceDetailCommand"));
    }

    [Fact]
    public void Serial_trace_detail_window_is_read_only_and_complete()
    {
        var path = Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", "SerialTraceDetailWindow.xaml");
        Assert.True(File.Exists(path));
        var xaml = File.ReadAllText(path);
        foreach (var heading in new[] { "TỔNG QUAN", "THÔNG TIN NHẬP", "THÔNG TIN XUẤT", "THÔNG TIN BẢO HÀNH" })
        {
            Assert.Contains(heading, xaml);
        }
        Assert.DoesNotContain("TextBox", xaml);
        Assert.DoesNotContain("DatePicker", xaml);
    }

    [Theory]
    [InlineData("AdjustmentView.xaml")]
    [InlineData("InventoryView.xaml")]
    [InlineData("OpeningBalanceImportView.xaml")]
    [InlineData("StockAdjustmentView.xaml")]
    [InlineData("StockCountView.xaml")]
    [InlineData("StockReversalView.xaml")]
    public void Main_view_headers_use_standard_h1_color(string fileName)
    {
        var document = LoadView(fileName);
        var heading = document.Descendants().First(element =>
            (string?)element.Attribute("Style") == "{StaticResource TypographyH1}");

        Assert.Null(heading.Attribute("Foreground"));
    }

    [Theory]
    [InlineData("PurchaseInvoiceView.xaml", "CartOutline")]
    [InlineData("SalesInvoiceView.xaml", "FileDocumentOutline")]
    public void Invoice_headers_use_standard_icon_layout(string fileName, string iconKind)
    {
        var document = LoadView(fileName);
        var icon = document.Descendants().First(element =>
            element.Name.LocalName == "PackIcon" && (string?)element.Attribute("Kind") == iconKind);

        Assert.Equal("32", (string?)icon.Attribute("Width"));
        Assert.Equal("{StaticResource AppTertiaryBrush}", (string?)icon.Attribute("Foreground"));
        Assert.Contains("{StaticResource PagePadding}", File.ReadAllText(Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", fileName)));
    }

    [Fact]
    public void Change_password_header_matches_sidebar_icon()
    {
        var document = LoadView("ChangePasswordView.xaml");
        var icon = document.Descendants().First(element => element.Name.LocalName == "PackIcon");

        Assert.Equal("KeyVariant", (string?)icon.Attribute("Kind"));
        Assert.Equal("32", (string?)icon.Attribute("Width"));
        Assert.Equal("{StaticResource AppTertiaryBrush}", (string?)icon.Attribute("Foreground"));
    }

    [Theory]
    [InlineData("DashboardView.xaml", "ViewDashboardOutline")]
    [InlineData("ReportView.xaml", "ChartBoxOutline")]
    public void Remaining_main_headers_use_standard_icon_size(string fileName, string iconKind)
    {
        var document = LoadView(fileName);
        var icon = document.Descendants().First(element =>
            element.Name.LocalName == "PackIcon" && (string?)element.Attribute("Kind") == iconKind);

        Assert.Equal("32", (string?)icon.Attribute("Width"));
        Assert.Equal("32", (string?)icon.Attribute("Height"));
        Assert.Equal("{StaticResource AppTertiaryBrush}", (string?)icon.Attribute("Foreground"));
    }

    [Theory]
    [InlineData("AppUserView.xaml")]
    [InlineData("AuditLogView.xaml")]
    [InlineData("BrandView.xaml")]
    [InlineData("CategoryView.xaml")]
    [InlineData("CustomerView.xaml")]
    [InlineData("InventoryView.xaml")]
    [InlineData("SupplierView.xaml")]
    [InlineData("UnitView.xaml")]
    [InlineData("WarrantyCoverageView.xaml")]
    [InlineData("WarrantyView.xaml")]
    public void Main_view_page_padding_is_not_shifted_by_root_max_width(string fileName)
    {
        var document = LoadView(fileName);
        var rootGrid = document.Descendants().First(element =>
            element.Name.LocalName == "Grid"
            && (string?)element.Attribute("Margin") == "{StaticResource PagePadding}");

        Assert.Equal("{StaticResource PagePadding}", (string?)rootGrid.Attribute("Margin"));
        Assert.Null(rootGrid.Attribute("MaxWidth"));
    }

    public static TheoryData<string> NavigableMainViews => new()
    {
        "DashboardView.xaml",
        "CategoryView.xaml",
        "BrandView.xaml",
        "UnitView.xaml",
        "SupplierView.xaml",
        "CustomerView.xaml",
        "ProductView.xaml",
        "ProductSerialView.xaml",
        "StockInView.xaml",
        "StockOutView.xaml",
        "StockTransferView.xaml",
        "StockAdjustmentView.xaml",
        "StockReversalView.xaml",
        "AdjustmentView.xaml",
        "InventoryView.xaml",
        "OpeningBalanceImportView.xaml",
        "StockCountView.xaml",
        "PurchaseInvoiceView.xaml",
        "SalesInvoiceView.xaml",
        "WarrantyCoverageView.xaml",
        "WarrantyView.xaml",
        "ReportView.xaml",
        "AppUserView.xaml",
        "AuditLogView.xaml",
        "ChangePasswordView.xaml"
    };

    [Theory]
    [MemberData(nameof(NavigableMainViews))]
    public void Navigable_views_use_the_same_page_header_contract(string fileName)
    {
        var document = LoadView(fileName);
        var pageGrid = document.Descendants().First(element =>
            element.Name.LocalName == "Grid"
            && (string?)element.Attribute("Margin") == "{StaticResource PagePadding}");
        Assert.Null(pageGrid.Attribute("MaxWidth"));

        var title = pageGrid.Descendants().First(element =>
            element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Style"))?.Contains("TypographyH1", StringComparison.Ordinal) == true);
        Assert.Null(title.Attribute("Foreground"));

        var titleGroup = title.Parent!;
        Assert.Equal("StackPanel", titleGroup.Name.LocalName);
        Assert.Equal("16,0,0,0", (string?)titleGroup.Attribute("Margin"));

        var headerContent = titleGroup.Parent!;
        var icon = headerContent.Elements().First(element => element.Name.LocalName == "PackIcon");
        Assert.Equal("32", (string?)icon.Attribute("Width"));
        Assert.Equal("32", (string?)icon.Attribute("Height"));
        Assert.Equal("{StaticResource AppTertiaryBrush}", (string?)icon.Attribute("Foreground"));

        var subtitle = titleGroup.Descendants().First(element =>
            element.Name.LocalName == "TextBlock"
            && !ReferenceEquals(element, title));
        Assert.Contains("TypographyCaption", (string?)subtitle.Attribute("Style"));
        Assert.Equal("0.7", (string?)subtitle.Attribute("Opacity"));

        var header = titleGroup.Ancestors().First(element => (string?)element.Attribute("Grid.Row") == "0");
        Assert.Equal("{StaticResource SectionBottomMargin}", (string?)header.Attribute("Margin"));
    }

    [Fact]
    public void Warranty_processing_panel_is_a_centered_padded_modal()
    {
        var document = LoadView("WarrantyView.xaml");
        var title = document.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "XỬ LÝ PHIẾU BẢO HÀNH");
        var modal = title.Ancestors().First(element =>
            element.Name.LocalName == "Border" && element.Attribute("Width") != null);

        Assert.Equal("760", (string?)modal.Attribute("Width"));
        Assert.Equal("820", (string?)modal.Attribute("MaxHeight"));
        Assert.Equal("Center", (string?)modal.Attribute("HorizontalAlignment"));
        Assert.Equal("Center", (string?)modal.Attribute("VerticalAlignment"));
        Assert.Equal("{StaticResource PagePadding}", (string?)modal.Attribute("Margin"));

        var scrollViewer = modal.Descendants().Single(element => element.Name.LocalName == "ScrollViewer");
        Assert.Null(scrollViewer.Attribute("Padding"));
        var contentBorder = scrollViewer.Elements().Single();
        Assert.Equal("Border", contentBorder.Name.LocalName);
        Assert.Equal("24", (string?)contentBorder.Attribute("Padding"));
    }

    [Fact]
    public void Serial_trace_detail_body_has_real_content_padding()
    {
        var document = LoadView("SerialTraceDetailWindow.xaml");
        var scrollViewer = document.Descendants().Single(element => element.Name.LocalName == "ScrollViewer");
        var contentBorder = scrollViewer.Elements().Single();

        Assert.Equal("Border", contentBorder.Name.LocalName);
        Assert.Equal("24", (string?)contentBorder.Attribute("Padding"));
    }

    [Theory]
    [InlineData(false, null, "Chưa bán")]
    [InlineData(true, null, "Không có bảo hành")]
    [InlineData(true, "Active", "Còn bảo hành")]
    [InlineData(true, "Expired", "Hết hạn bảo hành")]
    public void Warranty_trace_status_is_vietnamese(bool hasStockOut, string? coverageStatus, string expected)
    {
        var method = typeof(ReportTraceService).GetMethod("GetWarrantyStatus", BindingFlags.NonPublic | BindingFlags.Static)!;
        var warranty = coverageStatus == null ? null : new WarrantyCoverage
        {
            CoverageStatus = coverageStatus,
            WarrantyEndDate = coverageStatus == "Active" ? DateTime.Today.AddDays(1) : DateTime.Today.AddDays(-1)
        };

        Assert.Equal(expected, method.Invoke(null, new object?[] { hasStockOut, warranty }));
    }

    private static XDocument LoadView(string fileName) =>
        XDocument.Load(Path.Combine(RepoRoot, "QuanLyHangHoa", "Views", fileName));

    [Fact]
    public void Structural_filter_labels_are_semibold()
    {
        var typography = XDocument.Load(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Themes", "Typography.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var labelStyle = typography.Descendants().Single(element =>
            element.Name.LocalName == "Style"
            && (string?)element.Attribute(x + "Key") == "TypographyLabel");
        var fontWeight = labelStyle.Elements().Single(element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "FontWeight");

        Assert.Equal("SemiBold", (string?)fontWeight.Attribute("Value"));
    }
}
