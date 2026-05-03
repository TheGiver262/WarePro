using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class InvoiceServiceTests
{
    [Fact]
    public void SaveSalesInvoice_calculates_line_totals_and_invoice_totals()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product { Id = 900, ProductCode = "P900",
                DisplayName = "Invoice product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 100m
                 });
            seedContext.SaveChanges();
        }

        var service = new InvoiceService(() => CreateContext(connection));
        var invoice = new SalesInvoice
        {
            InvoiceCode = "SI-0001",
            CustomerId = 1,
            InvoiceDate = new DateTime(2026, 4, 28, 9, 0, 0),
            Lines = new List<SalesInvoiceLine>
            {
                new SalesInvoiceLine
                {
                    ProductId = 900,
                    UnitId = 1,
                    Quantity = 2,
                    UnitPrice = 100m,
                    TaxRate = 0.10m
                }
            }
        };

        service.SaveSalesInvoice(invoice);

        using var assertContext = CreateContext(connection);
        var saved = assertContext.SalesInvoices.Include(i => i.Lines).Single();
        Assert.Equal(200m, saved.SubTotal);
        Assert.Equal(20m, saved.TaxAmount);
        Assert.Equal(220m, saved.GrandTotal);

        var line = Assert.Single(saved.Lines!);
        Assert.Equal(200m, line.SubTotal);
        Assert.Equal(20m, line.TaxAmount);
        Assert.Equal(220m, line.GrandTotal);
    }

    [Fact]
    public void SavePurchaseInvoice_calculates_totals_correctly()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product { Id = 901, ProductCode = "P901",
                DisplayName = "Purchase invoice product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 100m
                 });
            seedContext.SaveChanges();
        }

        var service = new InvoiceService(() => CreateContext(connection));
        var invoice = new PurchaseInvoice
        {
            InvoiceCode = "PI-0001",
            SupplierId = 1,
            InvoiceDate = new DateTime(2026, 4, 28, 9, 30, 0),
            Lines = new List<PurchaseInvoiceLine>
            {
                new PurchaseInvoiceLine
                {
                    ProductId = 901,
                    UnitId = 1,
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxRate = 0.10m
                }
            }
        };

        service.SavePurchaseInvoice(invoice);

        using var assertContext = CreateContext(connection);
        var saved = assertContext.PurchaseInvoices.Include(i => i.Lines).Single();
        Assert.Equal(100m, saved.SubTotal);
        Assert.Equal(10m, saved.TaxAmount);
        Assert.Equal(110m, saved.GrandTotal);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
