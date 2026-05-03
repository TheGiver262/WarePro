using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class OpeningBalanceImportServiceTests
{
    [Fact]
    public void ImportRows_posts_serial_and_non_serial_opening_balances()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedProducts(seedContext);
        }

        var service = new OpeningBalanceImportService(() => CreateContext(connection));

        var result = service.ImportRows(new[]
        {
            new OpeningBalanceImportRow
            {
                RowNumber = 2,
                ProductId = 1400,
                
                SerialNumbers = string.Empty
            },
            new OpeningBalanceImportRow
            {
                RowNumber = 3,
                ProductId = 1401,
                
                SerialNumbers = "SER-001,SER-002"
            }
        }, postedByUserId: 9);

        Assert.Equal(2, result.SuccessCount);
        Assert.Empty(result.Errors);

        using var assertContext = CreateContext(connection);
        var balances = assertContext.StockBalances.OrderBy(balance => balance.ProductId).ToList();
        Assert.Equal(2, balances.Count);
        Assert.Equal(3, balances[0].OnHandQuantity);
        Assert.Equal(3, balances[0].AvailableQuantity);
        Assert.Equal(2, balances[1].OnHandQuantity);
        Assert.Equal(2, balances[1].AvailableQuantity);
        Assert.Equal(2, assertContext.ProductSerials.Count());
        Assert.Equal(2, assertContext.StockLedgers.Count());
        Assert.All(assertContext.AuditLogs, audit => Assert.Equal(AuditActionCode.PostStockIn.ToString(), audit.ActionCode));
    }

    [Fact]
    public void ImportRows_reports_invalid_row_without_posting_it()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedProducts(seedContext);
        }

        var service = new OpeningBalanceImportService(() => CreateContext(connection));

        var result = service.ImportRows(new[]
        {
            new OpeningBalanceImportRow
            {
                RowNumber = 5,
                ProductId = 1401,
                
                SerialNumbers = "ONLY-ONE"
            }
        }, postedByUserId: 9);

        Assert.Equal(0, result.SuccessCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(5, error.RowNumber);
        Assert.Equal("Serial count must match stock-in quantity.", error.ErrorMessage);

        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.StockBalances);
        Assert.Empty(assertContext.ProductSerials);
        Assert.Empty(assertContext.StockLedgers);
    }

    private static void SeedProducts(AppDbContext context)
    {
        context.Products.AddRange(
            new Product { Id = 1400, ProductCode = "P1400",
                DisplayName = "Opening non serial",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                
                DefaultPrice = 10m,
                IsSerialTracked = false
                 },
            new Product { Id = 1401, ProductCode = "P1401",
                DisplayName = "Opening serial",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                
                DefaultPrice = 10m,
                IsSerialTracked = true
                 });
        context.SaveChanges();
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
