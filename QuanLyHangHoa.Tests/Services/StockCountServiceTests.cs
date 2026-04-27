using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockCountServiceTests
{
    [Fact]
    public void CreateAdjustmentForDifferences_creates_draft_adjustment_without_changing_stock()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 600,
                Name = "Count product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = false
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 600,
                WarehouseId = 1,
                OnHandQuantity = 5,
                AvailableQuantity = 5
            });
            var session = new StockCountSession
            {
                SessionCode = "COUNT-0001",
                WarehouseId = 1,
                Status = StockDocumentStatus.Approved.ToString(),
                CountDate = new DateTime(2026, 4, 27, 15, 0, 0),
                CreatedBy = 1,
                Lines =
                {
                    new StockCountLine
                    {
                        ProductId = 600,
                        SystemQuantity = 5,
                        CountedQuantity = 7,
                        DifferenceQuantity = 2
                    }
                }
            };
            seedContext.StockCountSessions.Add(session);
            seedContext.SaveChanges();
            sessionId = session.Id;
        }

        var service = new StockCountService(() => CreateContext(connection));

        var adjustmentId = service.CreateAdjustmentForDifferences(sessionId, createdBy: 1);

        using var assertContext = CreateContext(connection);
        var adjustment = assertContext.StockAdjustments
            .Include(a => a.Lines)
            .Single(a => a.Id == adjustmentId);
        Assert.Equal("COUNT-0001", adjustment.ReferenceDocumentCode);
        Assert.Equal(StockDocumentStatus.Draft.ToString(), adjustment.Status);
        Assert.Equal("StockCount", adjustment.AdjustmentType);
        var line = Assert.Single(adjustment.Lines);
        Assert.Equal(600, line.ProductId);
        Assert.Equal("In", line.Direction);
        Assert.Equal(2m, line.QuantityDelta);

        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(5, balance.OnHandQuantity);
        Assert.Equal(5, balance.AvailableQuantity);
        Assert.Empty(assertContext.StockLedgers);
        Assert.Empty(assertContext.AuditLogs);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
