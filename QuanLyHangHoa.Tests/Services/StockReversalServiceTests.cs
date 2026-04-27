using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockReversalServiceTests
{
    [Fact]
    public void ReversePostedLedgerDocument_creates_and_posts_inverse_adjustment()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var originalDocumentId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 700,
                Name = "Reversal product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = false
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 700,
                WarehouseId = 1,
                OnHandQuantity = 10,
                AvailableQuantity = 10
            });
            seedContext.StockLedgers.Add(new StockLedger
            {
                DocumentId = originalDocumentId,
                ProductId = 700,
                WarehouseId = 1,
                Direction = StockLedgerDirection.In.ToString(),
                Quantity = 4,
                PostedAt = new DateTime(2026, 4, 27, 16, 0, 0),
                PostedByUserId = 1
            });
            seedContext.SaveChanges();
        }

        var service = new StockReversalService(() => CreateContext(connection));

        var reversalAdjustmentId = service.ReversePostedLedgerDocument(
            originalDocumentId,
            "Wrong stock-in document",
            reversedBy: 1);

        using var assertContext = CreateContext(connection);
        var adjustment = assertContext.StockAdjustments
            .Include(a => a.Lines)
            .Single(a => a.Id == reversalAdjustmentId);
        Assert.Equal(StockDocumentStatus.Posted.ToString(), adjustment.Status);
        Assert.Equal("Reversal", adjustment.AdjustmentType);
        Assert.Equal(originalDocumentId.ToString(), adjustment.ReferenceDocumentCode);
        var adjustmentLine = Assert.Single(adjustment.Lines);
        Assert.Equal(StockLedgerDirection.Out.ToString(), adjustmentLine.Direction);
        Assert.Equal(-4m, adjustmentLine.QuantityDelta);

        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(6, balance.OnHandQuantity);
        Assert.Equal(6, balance.AvailableQuantity);

        Assert.Equal(2, assertContext.StockLedgers.Count());
        Assert.Contains(assertContext.StockLedgers, ledger =>
            ledger.Direction == StockLedgerDirection.Out.ToString() && ledger.Quantity == 4);
        Assert.Single(assertContext.AuditLogs);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
