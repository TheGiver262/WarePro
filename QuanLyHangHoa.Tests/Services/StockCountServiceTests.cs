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
    public void CreateApprovedSession_calculates_system_and_difference_quantities()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 601,
                Name = "Counted product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = false
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 601,
                WarehouseId = 1,
                OnHandQuantity = 8,
                AvailableQuantity = 8
            });
            seedContext.SaveChanges();
        }

        var service = new StockCountService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 28, 9, 0, 0));

        var sessionId = service.CreateApprovedSession(
            "COUNT-APP-001",
            warehouseId: 1,
            countDate: new DateTime(2026, 4, 28),
            createdBy: 4,
            new[] { new StockCountInput(601, 6m) });

        using var assertContext = CreateContext(connection);
        var session = assertContext.StockCountSessions
            .Include(s => s.Lines)
            .Single(s => s.Id == sessionId);
        Assert.Equal("COUNT-APP-001", session.SessionCode);
        Assert.Equal(StockDocumentStatus.Approved.ToString(), session.Status);
        Assert.Equal(4, session.CreatedBy);
        Assert.Equal(4, session.ApprovedBy);
        Assert.Equal(new DateTime(2026, 4, 28, 9, 0, 0), session.ApprovedAt);

        var line = Assert.Single(session.Lines);
        Assert.Equal(601, line.ProductId);
        Assert.Equal(8m, line.SystemQuantity);
        Assert.Equal(6m, line.CountedQuantity);
        Assert.Equal(-2m, line.DifferenceQuantity);
    }

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
