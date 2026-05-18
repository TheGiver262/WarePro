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

public class ProductUnitServiceTests
{
    [Fact]
    public void Add_saves_product_unit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Units.AddRange(
                new Unit { Id = 101, UnitCode = "CAI", DisplayName = "Cai", IsActive = true },
                new Unit { Id = 102, UnitCode = "THUNG", DisplayName = "Thung", IsActive = true });
            seedContext.Products.Add(new Product { Id = 1200, ProductCode = "P1200",
                DisplayName = "Unit product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 101,
                DefaultPrice = 10m,
                IsActive = true
                 });
            seedContext.SaveChanges();
        }

        var service = new ProductUnitService(() => CreateContext(connection));

        service.Add(new ProductUnit
        {
            ProductId = 1200,
            UnitId = 102,
            ConversionFactor = 12m,
            IsBaseUnit = false
        });

        using var assertContext = CreateContext(connection);
        var productUnit = Assert.Single(assertContext.ProductUnits);
        Assert.Equal(1200, productUnit.ProductId);
        Assert.Equal(102, productUnit.UnitId);
        Assert.Equal(12m, productUnit.ConversionFactor);
        Assert.False(productUnit.IsBaseUnit);
    }

    [Fact]
    public void ConvertToBaseUnit_calculates_correct_quantity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.ProductUnits.Add(new ProductUnit
            {
                ProductId = 1200,
                UnitId = 102,
                ConversionFactor = 12m
            });
            seedContext.SaveChanges();
        }

        var service = new ProductUnitService(() => CreateContext(connection));
        var result = service.ConvertToBaseUnit(1200, 102, 2);
        Assert.Equal(24m, result);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }
}
