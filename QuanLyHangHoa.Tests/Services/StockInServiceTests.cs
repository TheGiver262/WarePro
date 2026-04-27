using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockInServiceTests
{
    [Fact]
    public void Create_posts_purchase_to_stock_balance_ledger_and_audit_without_changing_product_quantity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 200,
                Name = "Service stock-in product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = false
            });
            seedContext.SaveChanges();
        }

        var service = new StockInService(() => CreateContext(connection));
        var stockIn = new StockIn
        {
            EmployeeId = 1,
            SupplierId = 1,
            ImportDate = new DateTime(2026, 4, 27, 11, 30, 0),
            StockInDetails =
            {
                new StockInDetail
                {
                    ProductId = 200,
                    Quantity = 3,
                    ImportPrice = 12m
                }
            }
        };

        service.Create(stockIn);

        using var assertContext = CreateContext(connection);
        var savedStockIn = Assert.Single(assertContext.StockIns.Include(s => s.StockInDetails));
        Assert.Equal(36m, savedStockIn.TotalAmount);
        Assert.Equal(99, assertContext.Products.Single(p => p.Id == 200).Quantity);

        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(200, balance.ProductId);
        Assert.Equal(1, balance.WarehouseId);
        Assert.Equal(3, balance.OnHandQuantity);
        Assert.Equal(3, balance.AvailableQuantity);

        var ledger = Assert.Single(assertContext.StockLedgers);
        Assert.Equal(StockLedgerDirection.In.ToString(), ledger.Direction);
        Assert.Equal(3, ledger.Quantity);
        Assert.Equal(1, ledger.PostedByUserId);

        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Equal(AuditActionCode.PostStockIn.ToString(), audit.ActionCode);
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
