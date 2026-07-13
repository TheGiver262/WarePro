using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
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
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 700, ProductCode = "P700",
                DisplayName = "Reversal product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = false
                 });
            seedContext.StockIns.Add(new StockIn
            {
                Id = originalDocumentId,
                DocumentCode = "PIN-LEGACY-REVERSAL",
                WarehouseId = 1,
                PurposeCode = "Purchase",
                Status = "Posted",
                CreatedBy = 1,
                PostedBy = 1,
                CreatedAt = new DateTime(2026, 4, 27, 15, 0, 0),
                PostedAt = new DateTime(2026, 4, 27, 16, 0, 0)
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                WarehouseId = 1,
                ProductId = 700,
                OnHandQuantity = 10m,
                AvailableQuantity = 10m
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

        var service = new StockReversalService(() => DatabaseHelper.CreateContext(connection));

        service.ReverseDocument("StockIn", originalDocumentId, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var adjustment = assertContext.StockAdjustments.Single();
        Assert.Equal("Reversal", adjustment.AdjustmentType);
        Assert.Equal(originalDocumentId, adjustment.ReferenceDocumentId);

        var ledgerEntries = assertContext.StockLedgers.ToList();
        Assert.Equal(2, ledgerEntries.Count);
        
        var reversalEntry = ledgerEntries.Single(l => l.SourceDocumentType == "StockAdjustment");
        Assert.Equal("Out", reversalEntry.MovementType);
        Assert.Equal(10, reversalEntry.Quantity);
    }
}
