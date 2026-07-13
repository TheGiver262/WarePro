using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.ViewModels;

public static class InvoiceLinkedDocumentMapper
{
    public static List<SalesInvoiceLineEditor> MapSales(
        IEnumerable<StockOutLine> sourceLines,
        IEnumerable<Product> availableProducts)
    {
        var products = availableProducts.ToDictionary(product => product.Id);
        return sourceLines.Select(line => new SalesInvoiceLineEditor
        {
            SelectedProduct = GetProduct(products, line.ProductId),
            SourceLineId = line.Id,
            SourceUnitId = line.UnitId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            TaxRate = 0m
        }).ToList();
    }

    public static List<PurchaseInvoiceLineEditor> MapPurchase(
        IEnumerable<StockInLine> sourceLines,
        IEnumerable<Product> availableProducts)
    {
        var products = availableProducts.ToDictionary(product => product.Id);
        return sourceLines.Select(line => new PurchaseInvoiceLineEditor
        {
            SelectedProduct = GetProduct(products, line.ProductId),
            SourceLineId = line.Id,
            SourceUnitId = line.UnitId,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            TaxRate = 0m
        }).ToList();
    }

    private static Product GetProduct(IReadOnlyDictionary<int, Product> products, int productId) =>
        products.TryGetValue(productId, out var product)
            ? product
            : throw new InvalidOperationException($"Linked stock line product {productId} is not available.");
}
