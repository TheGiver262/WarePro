using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class InvoicePaymentServiceTests
{
    [Fact]
    public void RecordSalesPayment_adds_payment_and_marks_invoice_partial()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int invoiceId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            var seedInvoice = new SalesInvoice
            {
                InvoiceCode = "SI-PAY-001",
                CustomerId = 1,
                InvoiceDate = new DateTime(2026, 4, 29),
                DueDate = new DateTime(2026, 5, 29),
                GrandTotal = 500m,
                PaidAmount = 100m,
                PaymentStatus = "Partial"
            };
            seedContext.SalesInvoices.Add(seedInvoice);
            seedContext.SaveChanges();
            invoiceId = seedInvoice.Id;
        }

        var service = new InvoicePaymentService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 10, 0, 0));

        service.RecordSalesPayment(invoiceId, 150m, "Cash", "Thu dot 2", receivedBy: 7);

        using var assertContext = CreateContext(connection);
        var invoice = assertContext.SalesInvoices.Single(i => i.Id == invoiceId);
        Assert.Equal(250m, invoice.PaidAmount);
        Assert.Equal("Partial", invoice.PaymentStatus);
        var payment = Assert.Single(assertContext.InvoicePayments);
        Assert.Equal(invoiceId, payment.SalesInvoiceId);
        Assert.Null(payment.PurchaseInvoiceId);
        Assert.Equal(150m, payment.Amount);
        Assert.Equal("Cash", payment.PaymentMethod);
        Assert.Equal("Thu dot 2", payment.Note);
        Assert.Equal(7, payment.ReceivedBy);
        Assert.Equal(new DateTime(2026, 4, 29, 10, 0, 0), payment.PaymentDate);
    }

    [Fact]
    public void RecordSalesPayment_rejects_overpayment()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int invoiceId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            var invoice = new SalesInvoice
            {
                InvoiceCode = "SI-PAY-002",
                CustomerId = 1,
                InvoiceDate = new DateTime(2026, 4, 29),
                DueDate = new DateTime(2026, 5, 29),
                GrandTotal = 500m,
                PaidAmount = 450m,
                PaymentStatus = "Partial"
            };
            seedContext.SalesInvoices.Add(invoice);
            seedContext.SaveChanges();
            invoiceId = invoice.Id;
        }

        var service = new InvoicePaymentService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 10, 0, 0));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.RecordSalesPayment(invoiceId, 100m, "Cash", "Too much", receivedBy: 7));

        Assert.Equal("Payment amount exceeds remaining invoice balance.", ex.Message);
        using var assertContext = CreateContext(connection);
        Assert.Equal(450m, assertContext.SalesInvoices.Single(i => i.Id == invoiceId).PaidAmount);
        Assert.Empty(assertContext.InvoicePayments);
    }

    [Fact]
    public void RecordPurchasePayment_marks_invoice_paid_when_balance_is_cleared()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int invoiceId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            var seedInvoice = new PurchaseInvoice
            {
                InvoiceCode = "PI-PAY-001",
                SupplierId = 1,
                InvoiceDate = new DateTime(2026, 4, 29),
                DueDate = new DateTime(2026, 5, 29),
                GrandTotal = 700m,
                PaidAmount = 200m,
                PaymentStatus = "Partial"
            };
            seedContext.PurchaseInvoices.Add(seedInvoice);
            seedContext.SaveChanges();
            invoiceId = seedInvoice.Id;
        }

        var service = new InvoicePaymentService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 11, 0, 0));

        service.RecordPurchasePayment(invoiceId, 500m, "Bank", "Thanh toan het", paidBy: 8);

        using var assertContext = CreateContext(connection);
        var invoice = assertContext.PurchaseInvoices.Single(i => i.Id == invoiceId);
        Assert.Equal(700m, invoice.PaidAmount);
        Assert.Equal("Paid", invoice.PaymentStatus);
        var payment = Assert.Single(assertContext.InvoicePayments);
        Assert.Null(payment.SalesInvoiceId);
        Assert.Equal(invoiceId, payment.PurchaseInvoiceId);
        Assert.Equal(8, payment.ReceivedBy);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
