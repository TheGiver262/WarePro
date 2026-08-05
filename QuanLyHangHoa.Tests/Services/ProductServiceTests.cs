using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class ProductServiceTests
{
    [Fact]
    public async Task DeleteProduct_hard_deletes_pristine_product()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product
            {
                Id = 1099,
                ProductCode = "P1099",
                DisplayName = "Pristine product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true
            });
            seedContext.SaveChanges();
        }

        byte[] rowVersion;
        using (var db = CreateContext(connection))
            rowVersion = db.Products.AsNoTracking().Single(item => item.Id == 1099).RowVersion;
        var service = new ProductService(() => CreateContext(connection));
        await service.DeleteProductAsync(1099, rowVersion, userId: 1, Guid.NewGuid());

        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.Products);
        Assert.Equal("DELETE", Assert.Single(assertContext.AuditLogs).ActionCode);
    }

    [Fact]
    public void AddInitialStock_posts_opening_balance_without_changing_product_quantity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Single(user => user.Id == 1).RoleCode = "Quản trị viên";
            seedContext.Products.Add(new Product { Id = 1100, ProductCode = "P1100",
                DisplayName = "Initial stock product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                
                DefaultPrice = 10m,
                IsSerialTracked = true
                 });
            seedContext.SaveChanges();
        }

        var service = new ProductService(() => CreateContext(connection));

        service.AddInitialStockAsync(1100, new List<string> { "INIT-001", "INIT-002" }, userId: 1).GetAwaiter().GetResult();

        using var assertContext = CreateContext(connection);
        // Quantity removed
        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(2, balance.OnHandQuantity);
        Assert.Equal(2, balance.AvailableQuantity);
        Assert.Equal(2, assertContext.ProductSerials.Count());
        Assert.Single(assertContext.StockLedgers);
        Assert.Equal(AuditActionCode.PostStockIn.ToString(), Assert.Single(assertContext.AuditLogs).ActionCode);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }
}
