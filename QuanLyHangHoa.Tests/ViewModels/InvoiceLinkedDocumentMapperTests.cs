using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class InvoiceLinkedDocumentMapperTests
{
    [Fact]
    public void Sales_mapping_preserves_source_line_and_non_default_unit()
    {
        var product = Product();
        var source = new StockOutLine
        {
            Id = 41,
            ProductId = product.Id,
            Product = product,
            UnitId = 2,
            Quantity = 3,
            BaseQuantity = 30,
            UnitPrice = 125m
        };

        var editor = Assert.Single(InvoiceLinkedDocumentMapper.MapSales(
            new List<StockOutLine> { source },
            new List<Product> { product }));

        Assert.Same(product, editor.SelectedProduct);
        Assert.Equal(41, editor.SourceLineId);
        Assert.Equal(2, editor.SourceUnitId);
        Assert.Equal(3, editor.Quantity);
        Assert.Equal(125m, editor.UnitPrice);
    }

    [Fact]
    public void Purchase_mapping_preserves_source_line_and_non_default_unit()
    {
        var product = Product();
        var source = new StockInLine
        {
            Id = 51,
            ProductId = product.Id,
            Product = product,
            UnitId = 2,
            Quantity = 4,
            BaseQuantity = 40,
            UnitPrice = 80m
        };

        var editor = Assert.Single(InvoiceLinkedDocumentMapper.MapPurchase(
            new List<StockInLine> { source },
            new List<Product> { product }));

        Assert.Same(product, editor.SelectedProduct);
        Assert.Equal(51, editor.SourceLineId);
        Assert.Equal(2, editor.SourceUnitId);
        Assert.Equal(4, editor.Quantity);
        Assert.Equal(80m, editor.UnitPrice);
    }

    private static Product Product() => new()
    {
        Id = 910,
        ProductCode = "P910",
        DisplayName = "Mapped product",
        DefaultUnitId = 1,
        DefaultPrice = 999m
    };
}
