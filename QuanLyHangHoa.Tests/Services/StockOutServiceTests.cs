using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockOutServiceTests
{
    [Fact]
    public void Create_posts_sale_to_stock_balance_ledger_and_audit_without_changing_product_quantity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 300,
                Name = "Service stock-out product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = false
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 300,
                WarehouseId = 1,
                OnHandQuantity = 5,
                AvailableQuantity = 5,
                ReservedQuantity = 0
            });
            seedContext.SaveChanges();
        }

        var service = new StockOutService(() => CreateContext(connection));
        var stockOut = new StockOut
        {
            EmployeeId = 1,
            CustomerId = 1,
            ExportDate = new DateTime(2026, 4, 27, 12, 0, 0),
            StockOutDetails =
            {
                new StockOutDetail
                {
                    ProductId = 300,
                    Quantity = 2,
                    ExportPrice = 15m
                }
            }
        };

        service.Create(stockOut);

        using var assertContext = CreateContext(connection);
        var savedStockOut = Assert.Single(assertContext.StockOuts.Include(s => s.StockOutDetails));
        Assert.Equal(30m, savedStockOut.TotalAmount);
        Assert.Equal(99, assertContext.Products.Single(p => p.Id == 300).Quantity);

        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(300, balance.ProductId);
        Assert.Equal(3, balance.OnHandQuantity);
        Assert.Equal(3, balance.AvailableQuantity);

        var ledger = Assert.Single(assertContext.StockLedgers);
        Assert.Equal(StockLedgerDirection.Out.ToString(), ledger.Direction);
        Assert.Equal(2, ledger.Quantity);
        Assert.Equal(1, ledger.PostedByUserId);

        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Equal(AuditActionCode.PostStockOut.ToString(), audit.ActionCode);
        Assert.Equal(1, audit.PerformedByUserId);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
