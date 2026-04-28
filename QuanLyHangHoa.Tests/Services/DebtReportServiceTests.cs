using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class DebtReportServiceTests
{
    [Fact]
    public void GetCustomerDebtSummary_returns_unpaid_amount_grouped_by_customer()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.SalesInvoices.AddRange(
                new SalesInvoice
                {
                    InvoiceCode = "SI-RPT-001",
                    CustomerId = 1,
                    InvoiceDate = new DateTime(2026, 4, 28),
                    DueDate = new DateTime(2026, 5, 28),
                    GrandTotal = 220m,
                    PaidAmount = 50m,
                    PaymentStatus = "Partial"
                },
                new SalesInvoice
                {
                    InvoiceCode = "SI-RPT-002",
                    CustomerId = 1,
                    InvoiceDate = new DateTime(2026, 4, 28),
                    DueDate = new DateTime(2026, 5, 28),
                    GrandTotal = 100m,
                    PaidAmount = 100m,
                    PaymentStatus = "Paid"
                });
            seedContext.SaveChanges();
        }

        var service = new DebtReportService(() => CreateContext(connection));

        var summary = Assert.Single(service.GetCustomerDebtSummary());
        Assert.Equal(1, summary.PartyId);
        Assert.Equal("Kh\u00e1ch l\u1ebb", summary.PartyName);
        Assert.Equal(320m, summary.TotalAmount);
        Assert.Equal(150m, summary.PaidAmount);
        Assert.Equal(170m, summary.DebtAmount);
    }

    [Fact]
    public void GetSupplierDebtSummary_returns_unpaid_amount_grouped_by_supplier()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.PurchaseInvoices.Add(new PurchaseInvoice
            {
                InvoiceCode = "PI-RPT-001",
                SupplierId = 1,
                InvoiceDate = new DateTime(2026, 4, 28),
                DueDate = new DateTime(2026, 5, 28),
                GrandTotal = 500m,
                PaidAmount = 200m,
                PaymentStatus = "Partial"
            });
            seedContext.SaveChanges();
        }

        var service = new DebtReportService(() => CreateContext(connection));

        var summary = Assert.Single(service.GetSupplierDebtSummary());
        Assert.Equal(1, summary.PartyId);
        Assert.Equal(500m, summary.TotalAmount);
        Assert.Equal(200m, summary.PaidAmount);
        Assert.Equal(300m, summary.DebtAmount);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
