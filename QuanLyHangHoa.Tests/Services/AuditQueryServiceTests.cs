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
        var documentId = Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 800,
                Name = "Audit product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m
            });
            seedContext.StockLedgers.Add(new StockLedger
            {
                DocumentId = documentId,
                ProductId = 800,
                WarehouseId = 1,
                Direction = StockLedgerDirection.In.ToString(),
                Quantity = 2,
                PostedAt = new DateTime(2026, 4, 27, 16, 5, 0),
                PostedByUserId = 7
            });
            seedContext.AuditLogs.Add(new AuditLog
            {
                DocumentId = documentId,
                ActionCode = AuditActionCode.PostStockIn.ToString(),
                PerformedAt = new DateTime(2026, 4, 27, 16, 0, 0),
                PerformedByUserId = 7
            });
            seedContext.SaveChanges();
        }

        var service = new AuditQueryService(() => CreateContext(connection));

        var entries = service.GetDocumentTimeline(documentId);

        Assert.Equal(2, entries.Count);
        Assert.Equal(AuditTimelineEntryKind.Audit, entries[0].Kind);
        Assert.Equal(AuditActionCode.PostStockIn.ToString(), entries[0].Action);
        Assert.Equal(new DateTime(2026, 4, 27, 16, 0, 0), entries[0].OccurredAt);
        Assert.Equal(AuditTimelineEntryKind.StockLedger, entries[1].Kind);
        Assert.Equal(StockLedgerDirection.In.ToString(), entries[1].Action);
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
            seedContext.Products.Add(new Product
            {
                Id = 801,
                Name = "Ledger product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m
            });
            seedContext.StockLedgers.AddRange(
                new StockLedger
                {
                    DocumentId = Guid.Parse("bbbbbbbb-1111-2222-3333-bbbbbbbbbbbb"),
                    ProductId = 801,
                    WarehouseId = 1,
                    Direction = StockLedgerDirection.In.ToString(),
                    Quantity = 5,
                    PostedAt = new DateTime(2026, 4, 27, 9, 0, 0),
                    PostedByUserId = 7
                },
                new StockLedger
                {
                    DocumentId = Guid.Parse("cccccccc-1111-2222-3333-bbbbbbbbbbbb"),
                    ProductId = 801,
                    WarehouseId = 1,
                    Direction = StockLedgerDirection.Out.ToString(),
                    Quantity = 2,
                    PostedAt = new DateTime(2026, 4, 27, 10, 0, 0),
                    PostedByUserId = 8
                });
            seedContext.SaveChanges();
        }

        var service = new AuditQueryService(() => CreateContext(connection));

        var entries = service.GetProductLedger(801);

        Assert.Equal(2, entries.Count);
        Assert.Equal(StockLedgerDirection.Out.ToString(), entries[0].Action);
        Assert.Equal(2, entries[0].Quantity);
        Assert.Equal(StockLedgerDirection.In.ToString(), entries[1].Action);
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
