using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class ProductSerialServiceTests
{
    [Fact]
    public void SearchSerials_returns_matching_serial_with_product_and_warehouse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            SeedSerials(seedContext);
        }

        var service = new ProductSerialService(() => DatabaseHelper.CreateContext(connection));

        var serials = service.SearchSerials("ABC", string.Empty, string.Empty, "All");

        var serial = Assert.Single(serials);
        Assert.Equal("ABC-001", serial.SerialNumber);
        Assert.Equal("Serial product", serial.Product?.DisplayName);
        Assert.Equal("Main Warehouse", serial.CurrentWarehouse?.DisplayName);
    }

    [Fact]
    public void SearchSerials_filters_by_status()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            SeedSerials(seedContext);
        }

        var service = new ProductSerialService(() => DatabaseHelper.CreateContext(connection));

        var serials = service.SearchSerials(string.Empty, string.Empty, string.Empty, "Sold");

        var serial = Assert.Single(serials);
        Assert.Equal("SOLD-001", serial.SerialNumber);
        Assert.Equal("Sold", serial.CurrentStatus);
    }

    private static void SeedSerials(AppDbContext context)
    {
        context.Products.Add(new Product { Id = 1300, ProductCode = "P1300",
            DisplayName = "Serial product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsSerialTracked = true
             });

        context.StockIns.Add(new StockIn
        {
            Id = 500,
            DocumentCode = "IN001",
            WarehouseId = 1,
            PurposeCode = "Import",
            Status = "Posted",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        });

        context.StockInLines.Add(new StockInLine
        {
            Id = 600,
            StockInId = 500,
            ProductId = 1300,
            UnitId = 1,
            Quantity = 2,
            BaseQuantity = 2,
            UnitPrice = 10m
        });

        context.ProductSerials.AddRange(
            new ProductSerial { ProductId = 1300,
                SerialNumber = "ABC-001",
                CurrentStatus = "InStock",
                CurrentWarehouseId = 1,
                LastStockInLineId = 600
                 },
            new ProductSerial { ProductId = 1300,
                SerialNumber = "SOLD-001",
                CurrentStatus = "Sold",
                CurrentWarehouseId = null,
                LastStockInLineId = 600
                 });
        context.SaveChanges();
    }
}
