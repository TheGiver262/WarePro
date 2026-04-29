using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class ProductUnitServiceTests
{
    [Fact]
    public void AddProductUnit_rejects_non_positive_conversion_rate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedProductAndUnits(seedContext);
        }

        var service = new ProductUnitService(() => CreateContext(connection));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.AddProductUnit(new ProductUnit
            {
                ProductId = 1200,
                UnitId = 102,
                ConversionRateToBaseUnit = 0m,
                IsBaseUnit = false
            }));

        Assert.Equal("Conversion rate must be greater than zero.", ex.Message);
    }

    [Fact]
    public void AddProductUnit_rejects_second_base_unit_for_same_product()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedProductAndUnits(seedContext);
            seedContext.ProductUnits.Add(new ProductUnit
            {
                ProductId = 1200,
                UnitId = 101,
                ConversionRateToBaseUnit = 1m,
                IsBaseUnit = true
            });
            seedContext.SaveChanges();
        }

        var service = new ProductUnitService(() => CreateContext(connection));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.AddProductUnit(new ProductUnit
            {
                ProductId = 1200,
                UnitId = 102,
                ConversionRateToBaseUnit = 1m,
                IsBaseUnit = true
            }));

        Assert.Equal("Product already has a base unit.", ex.Message);
    }

    [Fact]
    public void AddProductUnit_saves_conversion_unit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedProductAndUnits(seedContext);
        }

        var service = new ProductUnitService(() => CreateContext(connection));

        service.AddProductUnit(new ProductUnit
        {
            ProductId = 1200,
            UnitId = 102,
            ConversionRateToBaseUnit = 12m,
            IsBaseUnit = false
        });

        using var assertContext = CreateContext(connection);
        var productUnit = Assert.Single(assertContext.ProductUnits);
        Assert.Equal(1200, productUnit.ProductId);
        Assert.Equal(102, productUnit.UnitId);
        Assert.Equal(12m, productUnit.ConversionRateToBaseUnit);
        Assert.False(productUnit.IsBaseUnit);
    }

    private static void SeedProductAndUnits(AppDbContext context)
    {
        context.Units.AddRange(
            new Unit { Id = 101, Name = "Cai" },
            new Unit { Id = 102, Name = "Thung" });
        context.Products.Add(new Product
        {
            Id = 1200,
            Name = "Unit product",
            CategoryId = 1,
            BrandId = 1,
            UnitId = 101,
            Quantity = 0,
            UnitPrice = 10m
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
