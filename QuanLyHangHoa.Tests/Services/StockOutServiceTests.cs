using System;
using System.Collections.Generic;
using System.Linq;
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
    public void Create_posts_to_database_and_updates_inventory()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product { Id = 300, ProductCode = "P300",
                DisplayName = "Service stock-out product",
                CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = false });
            seedContext.Warehouses.Add(new Warehouse { Id = 1, WarehouseCode = "WH001", DisplayName = "Default", IsDefault = true, IsActive = true });
            seedContext.StockBalances.Add(new StockBalance { ProductId = 300, WarehouseId = 1, OnHandQuantity = 5, AvailableQuantity = 5 });
            seedContext.SaveChanges();
        }

        var service = new StockOutService(() => CreateContext(connection));
        var stockOut = new StockOut
        {
            DocumentCode = "SO-001",
            CustomerId = 1,
            WarehouseId = 1,
            CreatedAt = new DateTime(2026, 4, 27, 12, 0, 0)
        };
        var lines = new List<StockOutLine>
        {
            new StockOutLine
            {
                ProductId = 300,
                UnitId = 1,
                Quantity = 2,
                UnitPrice = 15m
            }
        };

        service.Create(stockOut, lines, 1);

        using var assertContext = CreateContext(connection);
        var savedStockOut = assertContext.StockOuts.Include(s => s.Lines).Single();
        Assert.Equal("SO-001", savedStockOut.DocumentCode);
        Assert.Equal(1, savedStockOut.CreatedBy);

        var balance = assertContext.StockBalances.Single(b => b.ProductId == 300);
        Assert.Equal(3, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 300);
        Assert.Equal("Out", ledger.MovementType);
        Assert.Equal(2, ledger.Quantity);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
