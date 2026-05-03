using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockReversalServiceTests
{
    [Fact]
    public void ReverseDocument_creates_compensating_ledger_entries()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var originalDocumentId = 12345;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product { Id = 700, ProductCode = "P700",
                DisplayName = "Reversal product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = false
                 });
            seedContext.StockLedgers.Add(new StockLedger
            {
                SourceDocumentType = "StockIn",
                SourceDocumentId = originalDocumentId,
                ProductId = 700,
                WarehouseId = 1,
                MovementType = "In",
                Quantity = 10,
                PostedAt = new DateTime(2026, 4, 27, 16, 0, 0),
                PostedBy = 1
            });
            seedContext.SaveChanges();
        }

        var service = new StockReversalService(() => CreateContext(connection));

        service.ReverseDocument("StockIn", originalDocumentId, 1);

        using var assertContext = CreateContext(connection);
        var adjustment = assertContext.StockAdjustments.Single();
        Assert.Equal("Reversal", adjustment.AdjustmentType);
        Assert.Equal(originalDocumentId, adjustment.ReferenceDocumentId);

        var ledgerEntries = assertContext.StockLedgers.ToList();
        Assert.Equal(2, ledgerEntries.Count);
        
        var reversalEntry = ledgerEntries.Single(l => l.SourceDocumentType == "StockAdjustment");
        Assert.Equal("Out", reversalEntry.MovementType);
        Assert.Equal(10, reversalEntry.Quantity);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
