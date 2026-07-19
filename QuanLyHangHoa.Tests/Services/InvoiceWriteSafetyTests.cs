using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class InvoiceWriteSafetyTests
{
    [Fact]
    public async Task SaveSalesInvoiceAsync_verifies_uncertain_commit_without_duplicate()
    {
        await using var connection = new SqliteConnection(
            $"Data Source=invoice-write-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        await connection.OpenAsync();

        var setupOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using (var setup = new AppDbContext(setupOptions))
        {
            await setup.Database.EnsureCreatedAsync();
            DatabaseHelper.SeedBasicData(setup);
            setup.Products.Add(new Product
            {
                Id = 1,
                ProductCode = "P-INVOICE-WRITE",
                DisplayName = "Invoice write product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 100m
            });
            await setup.SaveChangesAsync();
        }

        var interceptor = new ThrowAfterFirstCommitInterceptor();
        var writeOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        AppDbContext CreateContext() => new(writeOptions);

        var service = new InvoiceService(CreateContext);
        var operationId = Guid.NewGuid();
        var invoice = new SalesInvoice
        {
            InvoiceCode = "SI-UNCERTAIN-COMMIT",
            CustomerId = 1,
            InvoiceDate = new DateTime(2026, 7, 18, 9, 0, 0),
            CreatedAt = new DateTime(2026, 7, 18, 9, 0, 0),
            Lines =
            [
                new SalesInvoiceLine
                {
                    ProductId = 1,
                    UnitId = 1,
                    Quantity = 1,
                    UnitPrice = 100m,
                    TaxRate = 0.1m
                }
            ]
        };

        var invoiceId = await service.SaveSalesInvoiceAsync(invoice, actorId: 1, operationId);

        await using var assertion = CreateContext();
        var saved = await assertion.SalesInvoices
            .Include(item => item.Lines)
            .SingleAsync(item => item.InvoiceCode == "SI-UNCERTAIN-COMMIT");
        Assert.Equal(invoiceId, saved.Id);
        Assert.Single(saved.Lines);
        Assert.Equal(1, await assertion.SalesInvoices.CountAsync());
    }

    private sealed class ThrowAfterFirstCommitInterceptor : DbTransactionInterceptor
    {
        private int _shouldThrow = 1;

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _shouldThrow, 0) == 1)
            {
                throw new InvalidOperationException("simulated uncertain commit");
            }

            return Task.CompletedTask;
        }
    }
}
