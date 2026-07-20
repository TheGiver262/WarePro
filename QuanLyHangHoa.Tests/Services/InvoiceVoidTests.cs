using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class InvoiceVoidTests
{
    [Fact]
    public async Task VoidSalesInvoiceAsync_keeps_invoice_lines_and_appends_reason()
    {
        using var connection = CreateDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewSalesInvoice("SI-VOID-001", "Ghi chú gốc");
        await service.SaveSalesInvoiceAsync(invoice, 1, Guid.NewGuid());

        await service.VoidSalesInvoiceAsync(
            invoice.Id,
            invoice.RowVersion,
            "Khách hàng yêu cầu hủy",
            1,
            Guid.NewGuid());

        using var assertion = DatabaseHelper.CreateContext(connection);
        var saved = assertion.SalesInvoices.Include(item => item.Lines).Single(item => item.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Voided, saved.Status);
        Assert.Contains("Ghi chú gốc", saved.Notes, StringComparison.Ordinal);
        Assert.Contains("[HỦY", saved.Notes, StringComparison.Ordinal);
        Assert.Contains("Khách hàng yêu cầu hủy", saved.Notes, StringComparison.Ordinal);
        Assert.Single(saved.Lines);
    }

    [Fact]
    public async Task VoidPurchaseInvoiceAsync_keeps_invoice_and_appends_reason()
    {
        using var connection = CreateDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewPurchaseInvoice("PI-VOID-001");
        await service.SavePurchaseInvoiceAsync(invoice, 1, Guid.NewGuid());

        await service.VoidPurchaseInvoiceAsync(
            invoice.Id,
            invoice.RowVersion,
            "Nhập nhầm chứng từ",
            1,
            Guid.NewGuid());

        using var assertion = DatabaseHelper.CreateContext(connection);
        var saved = assertion.PurchaseInvoices.Include(item => item.Lines).Single(item => item.Id == invoice.Id);
        Assert.Equal(InvoiceStatus.Voided, saved.Status);
        Assert.Contains("Nhập nhầm chứng từ", saved.Notes, StringComparison.Ordinal);
        Assert.Single(saved.Lines);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VoidSalesInvoiceAsync_rejects_blank_reason(string reason)
    {
        using var connection = CreateDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));

        await Assert.ThrowsAsync<ArgumentException>(() => service.VoidSalesInvoiceAsync(
            1,
            new byte[] { 1 },
            reason,
            1,
            Guid.NewGuid()));
    }

    [Fact]
    public async Task VoidSalesInvoiceAsync_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewSalesInvoice("SI-VOID-STALE");
        await service.SaveSalesInvoiceAsync(invoice, 1, Guid.NewGuid());
        var staleRowVersion = invoice.RowVersion.ToArray();

        using (var concurrent = DatabaseHelper.CreateContext(connection))
        {
            var changed = concurrent.SalesInvoices.Single(item => item.Id == invoice.Id);
            changed.Notes = "Máy khác đã sửa";
            await concurrent.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.VoidSalesInvoiceAsync(
            invoice.Id,
            staleRowVersion,
            "Hủy bằng bản cũ",
            1,
            Guid.NewGuid()));
    }

    [Fact]
    public async Task SaveSalesInvoiceAsync_rejects_edit_after_void()
    {
        using var connection = CreateDatabase();
        var service = new InvoiceService(() => DatabaseHelper.CreateContext(connection));
        var invoice = NewSalesInvoice("SI-VOID-LOCKED");
        await service.SaveSalesInvoiceAsync(invoice, 1, Guid.NewGuid());
        await service.VoidSalesInvoiceAsync(
            invoice.Id,
            invoice.RowVersion,
            "Sai khách hàng",
            1,
            Guid.NewGuid());

        using var read = DatabaseHelper.CreateContext(connection);
        var voided = read.SalesInvoices.AsNoTracking()
            .Include(item => item.Lines)
            .Single(item => item.Id == invoice.Id);
        voided.Notes = "Cố sửa sau hủy";

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveSalesInvoiceAsync(voided, 1, Guid.NewGuid()));
        Assert.Contains("voided", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var setup = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(setup);
        setup.Products.Add(new Product
        {
            Id = 990,
            ProductCode = "P-INVOICE-VOID",
            DisplayName = "Invoice void product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 100m
        });
        setup.SaveChanges();
        return connection;
    }

    private static SalesInvoice NewSalesInvoice(string code, string? notes = null) => new()
    {
        InvoiceCode = code,
        CustomerId = 1,
        InvoiceDate = new DateTime(2026, 7, 20, 9, 0, 0),
        CreatedAt = new DateTime(2026, 7, 20, 9, 0, 0),
        Notes = notes,
        Lines =
        [
            new SalesInvoiceLine
            {
                ProductId = 990,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 100m,
                TaxRate = 0m
            }
        ]
    };

    private static PurchaseInvoice NewPurchaseInvoice(string code) => new()
    {
        InvoiceCode = code,
        SupplierId = 1,
        InvoiceDate = new DateTime(2026, 7, 20, 9, 0, 0),
        CreatedAt = new DateTime(2026, 7, 20, 9, 0, 0),
        Lines =
        [
            new PurchaseInvoiceLine
            {
                ProductId = 990,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 100m,
                TaxRate = 0m
            }
        ]
    };
}
