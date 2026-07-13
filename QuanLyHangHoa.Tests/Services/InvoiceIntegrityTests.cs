using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class InvoiceIntegrityTests
{
    [Fact]
    public void SaveSalesInvoice_uses_canonical_partial_payment_status()
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewUnlinkedSalesInvoice("SI-PARTIAL", paidAmount: 50m);

        service.SaveSalesInvoice(invoice, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Equal(PaymentStatus.PartiallyPaid, assertContext.SalesInvoices.Single().PaymentStatus);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void SaveSalesInvoice_rejects_invalid_payment_amount(decimal paidAmount)
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewUnlinkedSalesInvoice("SI-BAD-PAYMENT", paidAmount);

        Assert.Throws<InvalidOperationException>(() => service.SaveSalesInvoice(invoice, 1));

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.SalesInvoices);
    }

    [Theory]
    [InlineData("Approved", "Sale", 1, 1)]
    [InlineData("Posted", "Adjustment", 1, 1)]
    [InlineData("Posted", "Sale", 2, 1)]
    [InlineData("Posted", "Sale", 1, 2)]
    public void SaveSalesInvoice_rejects_invalid_linked_stock_out(
        string status,
        string purpose,
        int customerId,
        decimal quantity)
    {
        using var connection = CreateInvoiceDatabase();
        using (var arrangeContext = DatabaseHelper.CreateContext(connection))
        {
            var stockOut = arrangeContext.StockOuts.Single(stockOut => stockOut.Id == 200);
            stockOut.Status = status;
            stockOut.PurposeCode = purpose;
            arrangeContext.SaveChanges();
        }

        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewLinkedSalesInvoice("SI-INVALID-LINK", 200, customerId, quantity);

        Assert.Throws<InvalidOperationException>(() => service.SaveSalesInvoice(invoice, 1));

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.SalesInvoices);
    }

    [Theory]
    [InlineData("Approved", "Purchase", 1, 1)]
    [InlineData("Posted", "OpeningBalance", 1, 1)]
    [InlineData("Posted", "Purchase", 2, 1)]
    [InlineData("Posted", "Purchase", 1, 2)]
    public void SavePurchaseInvoice_rejects_invalid_linked_stock_in(
        string status,
        string purpose,
        int supplierId,
        decimal quantity)
    {
        using var connection = CreateInvoiceDatabase();
        using (var arrangeContext = DatabaseHelper.CreateContext(connection))
        {
            var stockIn = arrangeContext.StockIns.Single(stockIn => stockIn.Id == 100);
            stockIn.Status = status;
            stockIn.PurposeCode = purpose;
            arrangeContext.SaveChanges();
        }

        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewLinkedPurchaseInvoice("PI-INVALID-LINK", 100, supplierId, quantity);

        Assert.Throws<InvalidOperationException>(() => service.SavePurchaseInvoice(invoice, 1));

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.PurchaseInvoices);
    }

    [Fact]
    public void SaveSalesInvoice_derives_linked_lines_and_prevents_stock_out_reuse()
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var first = NewLinkedSalesInvoice("SI-LINKED-1", 200, 1, 1);
        first.Lines.Single().UnitPrice = 999m;

        service.SaveSalesInvoice(first, 1);

        using (var assertContext = DatabaseHelper.CreateContext(connection))
        {
            var line = assertContext.SalesInvoiceLines.Single();
            Assert.Equal(201, line.StockOutLineId);
            Assert.Equal(100m, line.UnitPrice);
        }

        var reused = NewLinkedSalesInvoice("SI-LINKED-2", 200, 1, 1);
        Assert.Throws<InvalidOperationException>(() => service.SaveSalesInvoice(reused, 1));
    }

    [Fact]
    public void SavePurchaseInvoice_derives_linked_lines_and_prevents_stock_in_reuse()
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var first = NewLinkedPurchaseInvoice("PI-LINKED-1", 100, 1, 1);
        first.Lines.Single().UnitPrice = 999m;

        service.SavePurchaseInvoice(first, 1);

        using (var assertContext = DatabaseHelper.CreateContext(connection))
        {
            var line = assertContext.PurchaseInvoiceLines.Single();
            Assert.Equal(101, line.StockInLineId);
            Assert.Equal(80m, line.UnitPrice);
        }

        var reused = NewLinkedPurchaseInvoice("PI-LINKED-2", 100, 1, 1);
        Assert.Throws<InvalidOperationException>(() => service.SavePurchaseInvoice(reused, 1));
    }

    [Fact]
    public void SaveSalesInvoice_rolls_back_invoice_when_warranty_write_fails()
    {
        using var connection = CreateInvoiceDatabase();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailWarrantyInsert
                BEFORE INSERT ON WarrantyCoverage
                BEGIN
                    SELECT RAISE(ABORT, 'forced warranty failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewLinkedSalesInvoice("SI-ROLLBACK", 200, 1, 1);

        Assert.Throws<DbUpdateException>(() => service.SaveSalesInvoice(invoice, 1));

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.SalesInvoices);
        Assert.Empty(assertContext.WarrantyCoverages);
    }

    [Fact]
    public void SaveSalesInvoice_reconciles_retained_and_removed_warranty_coverages()
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewLinkedSalesInvoice("SI-RECONCILE", 200, 1, 1);
        invoice.InvoiceDate = new DateTime(2026, 1, 10);
        service.SaveSalesInvoice(invoice, 1);

        using (var arrangeContext = DatabaseHelper.CreateContext(connection))
        {
            arrangeContext.StockOuts.Single(stockOut => stockOut.Id == 200).CustomerId = 2;
            arrangeContext.SaveChanges();
        }

        var retainedUpdate = NewLinkedSalesInvoice("SI-RECONCILE", 200, 2, 1);
        retainedUpdate.Id = invoice.Id;
        retainedUpdate.InvoiceDate = new DateTime(2026, 2, 10);
        service.SaveSalesInvoice(retainedUpdate, 1);

        using (var retainedContext = DatabaseHelper.CreateContext(connection))
        {
            var retained = retainedContext.WarrantyCoverages.Single();
            Assert.Equal(2, retained.CustomerId);
            Assert.Equal(new DateTime(2026, 2, 10), retained.WarrantyStartDate);
            Assert.Equal(new DateTime(2027, 2, 10), retained.WarrantyEndDate);
            Assert.Equal("Active", retained.CoverageStatus);
        }

        var stockOutUpdate = NewLinkedSalesInvoice("SI-RECONCILE", 210, 2, 1);
        stockOutUpdate.Id = invoice.Id;
        stockOutUpdate.InvoiceDate = new DateTime(2026, 3, 10);
        service.SaveSalesInvoice(stockOutUpdate, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var coverages = assertContext.WarrantyCoverages.OrderBy(coverage => coverage.ProductSerialId).ToList();
        Assert.Equal(2, coverages.Count);
        Assert.Equal("Voided", coverages[0].CoverageStatus);
        Assert.Equal("Active", coverages[1].CoverageStatus);
        Assert.Equal(2, coverages[1].CustomerId);
        Assert.Equal(new DateTime(2026, 3, 10), coverages[1].WarrantyStartDate);
        Assert.Equal(new DateTime(2027, 3, 10), coverages[1].WarrantyEndDate);
    }

    [Fact]
    public void SaveSalesInvoice_preserves_transferred_replacement_coverage()
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewLinkedSalesInvoice("SI-TRANSFERRED", 200, 1, 1);
        invoice.InvoiceDate = new DateTime(2026, 1, 10);
        service.SaveSalesInvoice(invoice, 1);

        var replacementStart = new DateTime(2026, 5, 1);
        var replacementEnd = new DateTime(2026, 11, 1);
        int originalCoverageId;
        using (var arrangeContext = DatabaseHelper.CreateContext(connection))
        {
            var original = arrangeContext.WarrantyCoverages.Single();
            originalCoverageId = original.Id;
            original.CoverageStatus = "Inactive";
            arrangeContext.WarrantyCoverages.Add(new WarrantyCoverage
            {
                ProductSerialId = 301,
                CustomerId = 1,
                SalesInvoiceId = invoice.Id,
                WarrantyStartDate = replacementStart,
                WarrantyEndDate = replacementEnd,
                CoverageStatus = "Active"
            });
            arrangeContext.WarrantyClaims.Add(new WarrantyClaim
            {
                Id = 700,
                ClaimCode = "CLAIM-TRANSFERRED",
                WarrantyCoverageId = original.Id,
                ProductSerialId = 300,
                ReplacementSerialId = 301,
                ReceivedDate = new DateTime(2026, 4, 1),
                Status = "Closed",
                ProcessedBy = 1,
                ClosedDate = replacementStart
            });
            arrangeContext.SaveChanges();
        }

        var update = NewLinkedSalesInvoice("SI-TRANSFERRED", 200, 1, 1);
        update.Id = invoice.Id;
        update.InvoiceDate = new DateTime(2026, 6, 10);
        service.SaveSalesInvoice(update, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var originalCoverage = assertContext.WarrantyCoverages.Single(coverage => coverage.Id == originalCoverageId);
        var replacementCoverage = assertContext.WarrantyCoverages.Single(coverage => coverage.ProductSerialId == 301);
        Assert.Equal("Inactive", originalCoverage.CoverageStatus);
        Assert.Equal(new DateTime(2026, 1, 10), originalCoverage.WarrantyStartDate);
        Assert.Equal(new DateTime(2027, 1, 10), originalCoverage.WarrantyEndDate);
        Assert.Equal("Active", replacementCoverage.CoverageStatus);
        Assert.Equal(replacementStart, replacementCoverage.WarrantyStartDate);
        Assert.Equal(replacementEnd, replacementCoverage.WarrantyEndDate);
    }

    [Fact]
    public void SaveSalesInvoice_does_not_create_coverage_when_warranty_months_is_zero()
    {
        using var connection = CreateInvoiceDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));

        service.SaveSalesInvoice(NewLinkedSalesInvoice(
            "SI-NO-WARRANTY",
            stockOutId: 220,
            customerId: 1,
            quantity: 1,
            productId: 911), 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.WarrantyCoverages);
    }

    [Fact]
    public void SaveSalesInvoice_rejects_negative_warranty_period_without_writes()
    {
        using var connection = CreateInvoiceDatabase();
        using (var arrangeContext = DatabaseHelper.CreateContext(connection))
        {
            arrangeContext.Products.Single(product => product.Id == 910).WarrantyPeriodMonths = -1;
            arrangeContext.SaveChanges();
        }
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.SaveSalesInvoice(NewLinkedSalesInvoice(
                "SI-NEGATIVE-WARRANTY",
                stockOutId: 200,
                customerId: 1,
                quantity: 1), 1));

        Assert.Contains("warranty period", exception.Message, StringComparison.OrdinalIgnoreCase);
        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.SalesInvoices);
        Assert.Empty(assertContext.WarrantyCoverages);
    }

    [Fact]
    public void Query_marks_past_due_unpaid_invoice_as_effectively_overdue_without_persisting_it()
    {
        using var connection = CreateInvoiceDatabase();
        using (var arrangeContext = DatabaseHelper.CreateContext(connection))
        {
            var invoice = NewUnlinkedSalesInvoice("SI-STALE-OVERDUE", paidAmount: 0);
            invoice.DueDate = DateTime.Today.AddDays(-1);
            invoice.PaymentStatus = PaymentStatus.Unpaid;
            arrangeContext.SalesInvoices.Add(invoice);
            arrangeContext.SaveChanges();
        }

        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var result = Assert.Single(service.GetAllSalesInvoices());
        Assert.Equal(PaymentStatus.Overdue, result.PaymentStatus);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Equal(PaymentStatus.Unpaid, assertContext.SalesInvoices.Single().PaymentStatus);
    }

    private static SqliteConnection CreateInvoiceDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(context);
        context.Customers.Add(new Customer
        {
            Id = 2,
            CustomerCode = "CUST2",
            DisplayName = "Second Customer",
            IsActive = true
        });
        context.Suppliers.Add(new Supplier
        {
            Id = 2,
            SupplierCode = "SUP2",
            DisplayName = "Second Supplier",
            IsActive = true
        });
        context.Products.AddRange(
            new Product
            {
                Id = 910,
                ProductCode = "P910",
                DisplayName = "Warranty product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 100m,
                WarrantyPeriodMonths = 12,
                IsSerialTracked = true
            },
            new Product
            {
                Id = 911,
                ProductCode = "P911",
                DisplayName = "No warranty product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 50m,
                WarrantyPeriodMonths = 0,
                IsSerialTracked = true
            });
        context.StockIns.Add(new StockIn
        {
            Id = 100,
            DocumentCode = "STI-100",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase",
            Status = "Posted",
            CreatedBy = 1,
            CreatedAt = new DateTime(2026, 1, 1),
            Lines = new List<StockInLine>
            {
                new()
                {
                    Id = 101,
                    ProductId = 910,
                    UnitId = 1,
                    Quantity = 1,
                    BaseQuantity = 1,
                    UnitPrice = 80m
                }
            }
        });
        context.StockOuts.AddRange(
            NewStockOut(200, "STO-200", 1, 201, 910, 100m),
            NewStockOut(210, "STO-210", 2, 211, 910, 110m),
            NewStockOut(220, "STO-220", 1, 221, 911, 50m));
        context.SaveChanges();
        context.ProductSerials.AddRange(
            Serial(300, "SERIAL-A", 910, 201),
            Serial(301, "SERIAL-B", 910, 211),
            Serial(302, "SERIAL-NO-WARRANTY", 911, 221));
        context.SaveChanges();
        return connection;
    }

    private static StockOut NewStockOut(
        int id,
        string code,
        int customerId,
        int lineId,
        int productId,
        decimal unitPrice) => new()
    {
        Id = id,
        DocumentCode = code,
        CustomerId = customerId,
        WarehouseId = 1,
        PurposeCode = "Sale",
        Status = "Posted",
        CreatedBy = 1,
        CreatedAt = new DateTime(2026, 1, 1),
        Lines = new List<StockOutLine>
        {
            new()
            {
                Id = lineId,
                ProductId = productId,
                UnitId = 1,
                Quantity = 1,
                BaseQuantity = 1,
                UnitPrice = unitPrice
            }
        }
    };

    private static ProductSerial Serial(int id, string serialNumber, int productId, int stockOutLineId) => new()
    {
        Id = id,
        ProductId = productId,
        SerialNumber = serialNumber,
        CurrentStatus = "Sold",
        LastStockInLineId = 101,
        LastStockOutLineId = stockOutLineId
    };

    private static SalesInvoice NewUnlinkedSalesInvoice(string code, decimal paidAmount) => new()
    {
        InvoiceCode = code,
        CustomerId = 1,
        InvoiceDate = new DateTime(2026, 4, 28),
        PaidAmount = paidAmount,
        CreatedBy = 1,
        CreatedAt = new DateTime(2026, 4, 28),
        Lines = new List<SalesInvoiceLine>
        {
            new()
            {
                ProductId = 910,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 100m,
                TaxRate = 0m
            }
        }
    };

    private static SalesInvoice NewLinkedSalesInvoice(
        string code,
        int stockOutId,
        int customerId,
        decimal quantity,
        int productId = 910) => new()
    {
        InvoiceCode = code,
        CustomerId = customerId,
        StockOutId = stockOutId,
        InvoiceDate = new DateTime(2026, 4, 28),
        CreatedBy = 1,
        CreatedAt = new DateTime(2026, 4, 28),
        Lines = new List<SalesInvoiceLine>
        {
            new()
            {
                ProductId = productId,
                UnitId = 1,
                Quantity = quantity,
                UnitPrice = 100m,
                TaxRate = 0m
            }
        }
    };

    private static PurchaseInvoice NewLinkedPurchaseInvoice(
        string code,
        int stockInId,
        int supplierId,
        decimal quantity) => new()
    {
        InvoiceCode = code,
        SupplierId = supplierId,
        StockInId = stockInId,
        InvoiceDate = new DateTime(2026, 4, 28),
        CreatedBy = 1,
        CreatedAt = new DateTime(2026, 4, 28),
        Lines = new List<PurchaseInvoiceLine>
        {
            new()
            {
                ProductId = 910,
                UnitId = 1,
                Quantity = quantity,
                UnitPrice = 80m,
                TaxRate = 0m
            }
        }
    };
}
