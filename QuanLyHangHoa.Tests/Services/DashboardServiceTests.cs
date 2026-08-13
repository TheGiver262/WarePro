using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class DashboardServiceTests
{
    [Fact]
    public async Task Top_selling_uses_base_quantity_and_keeps_same_named_products_separate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            db.Products.AddRange(
                CreateProduct(100, "SAME-A", "Same name"),
                CreateProduct(101, "SAME-B", "Same name"));
            db.ProductUnits.Add(new ProductUnit
            {
                ProductId = 100,
                UnitId = 2,
                ConversionFactor = 10m,
                IsSalesUnit = true
            });
            var stockOut = new StockOut
            {
                Id = 100,
                DocumentCode = "SO-DASH",
                CustomerId = 1,
                WarehouseId = 1,
                PurposeCode = "Sale",
                Status = DocumentStatus.Posted,
                ExportDate = DateTime.Today,
                CreatedBy = 1,
                CreatedAt = DateTime.Now
            };
            var stockOutLine = new StockOutLine
            {
                Id = 100,
                StockOutId = stockOut.Id,
                ProductId = 100,
                UnitId = 2,
                Quantity = 99m,
                BaseQuantity = 10m,
                UnitPrice = 1m
            };
            db.StockOuts.Add(stockOut);
            db.StockOutLines.Add(stockOutLine);
            var invoice = new SalesInvoice
            {
                Id = 100,
                InvoiceCode = "INV-DASH",
                CustomerId = 1,
                InvoiceDate = DateTime.Today,
                Status = InvoiceStatus.Active,
                CreatedBy = 1,
                CreatedAt = DateTime.Now
            };
            db.SalesInvoices.Add(invoice);
            db.SalesInvoiceLines.AddRange(
                CreateInvoiceLine(100, invoice.Id, 100, 2, 1m, stockOutLine.Id),
                CreateInvoiceLine(101, invoice.Id, 100, 1, 5m),
                CreateInvoiceLine(102, invoice.Id, 101, 1, 7m));
            db.SaveChanges();
        }
        var service = new DashboardService(() => DatabaseHelper.CreateContext(connection));

        var result = await service.GetTopSellingProductsAsync(5);

        Assert.Equal(2, result.Count);
        Assert.Equal(100, result[0].ProductId);
        Assert.Equal("Same name", result[0].ProductName);
        Assert.Equal(15m, result[0].TotalSold);
        Assert.Equal(101, result[1].ProductId);
        Assert.Equal("Same name", result[1].ProductName);
        Assert.Equal(7m, result[1].TotalSold);
    }

    [Fact]
    public async Task Top_selling_rejects_missing_non_default_conversion()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            db.Products.Add(CreateProduct(110, "MISSING-UNIT", "Missing unit"));
            var invoice = new SalesInvoice
            {
                Id = 110,
                InvoiceCode = "INV-MISSING",
                CustomerId = 1,
                InvoiceDate = DateTime.Today,
                Status = InvoiceStatus.Active,
                CreatedBy = 1,
                CreatedAt = DateTime.Now
            };
            db.SalesInvoices.Add(invoice);
            db.SalesInvoiceLines.Add(CreateInvoiceLine(110, invoice.Id, 110, 2, 1m));
            db.SaveChanges();
        }
        var service = new DashboardService(() => DatabaseHelper.CreateContext(connection));

        await Assert.ThrowsAsync<InventoryDomainException>(() => service.GetTopSellingProductsAsync(5));
    }

    private static Product CreateProduct(int id, string code, string name) => new()
    {
        Id = id,
        ProductCode = code,
        DisplayName = name,
        CategoryId = 1,
        BrandId = 1,
        DefaultUnitId = 1,
        DefaultPrice = 1m,
        IsActive = true
    };

    private static SalesInvoiceLine CreateInvoiceLine(
        int id,
        int invoiceId,
        int productId,
        int unitId,
        decimal quantity,
        int? stockOutLineId = null) => new()
    {
        Id = id,
        SalesInvoiceId = invoiceId,
        ProductId = productId,
        UnitId = unitId,
        Quantity = quantity,
        StockOutLineId = stockOutLineId,
        UnitPrice = 1m,
        SubTotal = quantity,
        GrandTotal = quantity
    };
}
