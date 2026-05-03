using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class AuditQueryServiceTests
{
    [Fact]
    public void GetDocumentTimeline_returns_audit_and_ledger_entries_ordered_by_time()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var documentId = 123;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product { Id = 800, ProductCode = "P800",
                DisplayName = "Audit product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m
                 });
            seedContext.StockLedgers.Add(new StockLedger
            {
                SourceDocumentId = documentId,
                ProductId = 800,
                WarehouseId = 1,
                MovementType = "In",
                Quantity = 2,
                PostedAt = new DateTime(2026, 4, 27, 16, 5, 0),
                PostedBy = 7
            });
            seedContext.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockIn",
                EntityId = documentId,
                ActionCode = AuditActionCode.PostStockIn.ToString(),
                PerformedAt = new DateTime(2026, 4, 27, 16, 0, 0),
                PerformedBy = 7
            });
            seedContext.SaveChanges();
        }

        var service = new AuditQueryService(() => CreateContext(connection));

        var entries = service.GetEntityTimeline("StockIn", documentId);

        Assert.Equal(2, entries.Count);
        Assert.Equal(AuditTimelineEntryKind.Audit, entries[0].Kind);
        Assert.Equal(AuditActionCode.PostStockIn.ToString(), entries[0].Action);
        Assert.Equal(new DateTime(2026, 4, 27, 16, 0, 0), entries[0].OccurredAt);
        Assert.Equal(AuditTimelineEntryKind.StockLedger, entries[1].Kind);
        Assert.Equal("In", entries[1].Action);
        Assert.Equal(800, entries[1].ProductId);
        Assert.Equal(2, entries[1].Quantity);
    }

    [Fact]
    public void GetProductLedger_returns_product_movements_ordered_newest_first()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product { Id = 801, ProductCode = "P801",
                DisplayName = "Ledger product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                
                DefaultPrice = 10m
                 });
            seedContext.StockLedgers.AddRange(
                new StockLedger
                {
                    SourceDocumentId = 101,
                    ProductId = 801,
                    WarehouseId = 1,
                    MovementType = "In",
                    Quantity = 5,
                    PostedAt = new DateTime(2026, 4, 27, 9, 0, 0),
                    PostedBy = 7
                },
                new StockLedger
                {
                    SourceDocumentId = 102,
                    ProductId = 801,
                    WarehouseId = 1,
                    MovementType = "Out",
                    Quantity = 2,
                    PostedAt = new DateTime(2026, 4, 27, 10, 0, 0),
                    PostedBy = 8
                });
            seedContext.SaveChanges();
        }

        var service = new AuditQueryService(() => CreateContext(connection));

        var entries = service.GetProductLedger(801);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Out", entries[0].Action);
        Assert.Equal(2, entries[0].Quantity);
        Assert.Equal("In", entries[1].Action);
        Assert.Equal(5, entries[1].Quantity);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
