using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockAdjustmentServiceTests
{
    [Fact]
    public void Post_saves_adjustment_and_updates_stock_balance_ledger_and_audit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 500,
                Name = "Adjustment service product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = false
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 500,
                WarehouseId = 1,
                OnHandQuantity = 5,
                AvailableQuantity = 5
            });
            seedContext.SaveChanges();
        }

        var service = new StockAdjustmentService(() => CreateContext(connection));
        var adjustment = new StockAdjustment
        {
            DocumentCode = "ADJ-SVC-001",
            WarehouseId = 1,
            AdjustmentType = "Manual",
            Status = StockDocumentStatus.Approved.ToString(),
            ReferenceDocumentCode = "COUNT-SVC-001",
            ReasonCode = "CountDifference",
            CreatedBy = 1,
            PostedBy = 1,
            Lines =
            {
                new StockAdjustmentLine
                {
                    ProductId = 500,
                    QuantityDelta = -2m,
                    BaseQuantityDelta = -2m,
                    Direction = StockLedgerDirection.Out.ToString()
                }
            }
        };

        service.Post(adjustment);

        using var assertContext = CreateContext(connection);
        var savedAdjustment = Assert.Single(assertContext.StockAdjustments.Include(a => a.Lines));
        Assert.Equal(StockDocumentStatus.Posted.ToString(), savedAdjustment.Status);
        Assert.NotNull(savedAdjustment.PostedAt);
        Assert.Single(savedAdjustment.Lines);

        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(3, balance.OnHandQuantity);
        Assert.Equal(3, balance.AvailableQuantity);

        var ledger = Assert.Single(assertContext.StockLedgers);
        Assert.Equal(StockLedgerDirection.Out.ToString(), ledger.Direction);
        Assert.Equal(2, ledger.Quantity);

        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Equal(AuditActionCode.PostStockAdjustment.ToString(), audit.ActionCode);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
