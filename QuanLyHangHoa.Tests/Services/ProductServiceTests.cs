using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class ProductServiceTests
{
    [Fact]
    public void AddInitialStock_posts_opening_balance_without_changing_product_quantity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 1100,
                Name = "Initial stock product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = true
            });
            seedContext.SaveChanges();
        }

        var service = new ProductService(() => CreateContext(connection));

        service.AddInitialStock(1100, new List<string> { "INIT-001", "INIT-002" });

        using var assertContext = CreateContext(connection);
        Assert.Equal(99, assertContext.Products.Single(p => p.Id == 1100).Quantity);
        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(2, balance.OnHandQuantity);
        Assert.Equal(2, balance.AvailableQuantity);
        Assert.Equal(2, assertContext.ProductSerials.Count());
        Assert.Single(assertContext.StockLedgers);
        Assert.Equal(AuditActionCode.PostStockIn.ToString(), Assert.Single(assertContext.AuditLogs).ActionCode);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
