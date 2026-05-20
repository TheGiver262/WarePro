using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockInServiceTests
{
    [Fact]
    public void Create_posts_to_database_and_updates_inventory()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 200, ProductCode = "P200",
                DisplayName = "Service stock-in product",
                CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = false });
            seedContext.SaveChanges();
        }

        var service = new StockInService(() => CreateContext(connection));
        var stockIn = new StockIn
        {
            DocumentCode = "SI-001",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase",
            CreatedAt = new DateTime(2026, 4, 27, 11, 30, 0)
        };
        var lines = new List<StockInLine>
        {
            new StockInLine
            {
                ProductId = 200,
                UnitId = 1,
                Quantity = 3,
                UnitPrice = 12m
            }
        };

        service.SaveDraft(stockIn, lines, 1);
        service.Post(stockIn.Id, 1);

        using var assertContext = CreateContext(connection);
        var savedStockIn = assertContext.StockIns.Include(s => s.Lines).Single();
        Assert.Equal("SI-001", savedStockIn.DocumentCode);
        Assert.Equal(1, savedStockIn.CreatedBy);

        var balance = assertContext.StockBalances.Single(b => b.ProductId == 200);
        Assert.Equal(3, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 200);
        Assert.Equal("In", ledger.MovementType);
        Assert.Equal(3, ledger.Quantity);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }
}
