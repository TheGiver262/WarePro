using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class StockAdjustmentPersistenceTests
{
    [Fact]
    public void AppDbContext_persists_stock_adjustment_with_lines_in_sqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        context.Database.EnsureCreated();
        context.Products.Add(new Product { Id = 400, ProductCode = "P400",
            DisplayName = "Adjustment product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            
            DefaultPrice = 10m,
            IsSerialTracked = false
             });
        context.SaveChanges();

        context.StockAdjustments.Add(new StockAdjustment
        {
            DocumentCode = "ADJ-0001",
            WarehouseId = 1,
            AdjustmentType = "StockCount",
            Status = "Approved",
            ReferenceDocumentCode = "COUNT-0001",
            ReasonCode = "CountDifference",
            CreatedBy = 1,
            PostedBy = 1,
            PostedAt = new DateTime(2026, 4, 27, 14, 0, 0),
            Lines = new List<StockAdjustmentLine>
            {
                new StockAdjustmentLine
                {
                    ProductId = 400,
                    QuantityDelta = 3m,
                    BaseQuantityDelta = 3m,
                    Direction = "In"
                }
            }
        });
        context.SaveChanges();

        var adjustment = Assert.Single(context.StockAdjustments.Include(a => a.Lines));
        Assert.Equal("ADJ-0001", adjustment.DocumentCode);
        Assert.Equal(1, adjustment.WarehouseId);
        Assert.NotNull(adjustment.Lines);
        var line = Assert.Single(adjustment.Lines);
        Assert.Equal(400, line.ProductId);
        Assert.Equal(3m, line.QuantityDelta);
        Assert.Equal("In", line.Direction);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
