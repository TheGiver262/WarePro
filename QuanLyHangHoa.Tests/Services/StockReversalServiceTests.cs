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
    public async Task ReverseDocument_accepts_document_code_and_persists_reason()
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

        await service.ReverseDocumentAsync(
            "StockIn",
            "PIN-LEGACY-REVERSAL",
            "Nhập nhầm chứng từ",
            1,
            Guid.NewGuid());

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var adjustment = assertContext.StockAdjustments.Single();
        Assert.Equal("Reversal", adjustment.AdjustmentType);
        Assert.Equal(originalDocumentId, adjustment.ReferenceDocumentId);
        Assert.Equal("USER-REVERSAL", adjustment.ReasonCode);
        Assert.Equal("Nhập nhầm chứng từ", adjustment.Notes);

        var ledgerEntries = assertContext.StockLedgers.ToList();
        Assert.Equal(2, ledgerEntries.Count);
        
        var reversalEntry = ledgerEntries.Single(l => l.SourceDocumentType == "StockAdjustment");
        Assert.Equal("Out", reversalEntry.MovementType);
        Assert.Equal(10, reversalEntry.Quantity);
    }
    [Fact]
    public async Task ReverseDocument_query_count_does_not_grow_per_movement()
    {
        var singleMovementCount = await CountReverseSelectsAsync(1);
        var sixMovementCount = await CountReverseSelectsAsync(6);

        Assert.True(
            sixMovementCount <= singleMovementCount + 2,
            $"Expected at most {singleMovementCount + 2} SELECTs, but observed {sixMovementCount}.");
    }

    private static async Task<int> CountReverseSelectsAsync(int movementCount)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        const int documentId = 23456;
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.StockIns.Add(new StockIn
            {
                Id = documentId,
                DocumentCode = $"SI-N1-REV-{movementCount}",
                WarehouseId = 1,
                PurposeCode = "Purchase",
                Status = "Posted",
                CreatedBy = 1,
                PostedBy = 1,
                CreatedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow
            });

            foreach (var index in Enumerable.Range(0, movementCount))
            {
                var productId = 800 + index;
                seedContext.Products.Add(new Product
                {
                    Id = productId,
                    ProductCode = $"N1-REV-{index}",
                    DisplayName = $"N+1 reversal product {index}",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsActive = true,
                    IsSerialTracked = false
                });
                seedContext.StockBalances.Add(new StockBalance
                {
                    ProductId = productId,
                    WarehouseId = 1,
                    OnHandQuantity = 10m,
                    AvailableQuantity = 10m
                });
                seedContext.StockLedgers.Add(new StockLedger
                {
                    SourceDocumentType = "StockIn",
                    SourceDocumentId = documentId,
                    ProductId = productId,
                    WarehouseId = 1,
                    MovementType = "In",
                    Quantity = 1m,
                    PostedAt = DateTime.UtcNow,
                    PostedBy = 1
                });
            }
            seedContext.SaveChanges();
        }

        var counter = new SelectCommandCounter();
        var service = new StockReversalService(
            () => DatabaseHelper.CreateContext(connection, counter));

        await service.ReverseDocumentAsync(
            "StockIn", $"SI-N1-REV-{movementCount}", "N+1 query test", 1, Guid.NewGuid());

        return counter.Count;
    }
}
