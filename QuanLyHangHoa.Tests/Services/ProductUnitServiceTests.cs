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
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Add_rejects_non_positive_conversion_factor(decimal factor)
    {
        using var connection = CreateDatabase();
        var service = new ProductUnitService(() => CreateContext(connection));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(new ProductUnit
        {
            ProductId = 1200,
            UnitId = 102,
            ConversionFactor = factor
        }, actorId: 2, Guid.NewGuid()));

        Assert.Contains("greater than zero", exception.Message, StringComparison.OrdinalIgnoreCase);
        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.ProductUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Update_rejects_non_positive_conversion_factor(decimal factor)
    {
        using var connection = CreateDatabase();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.ProductUnits.Add(new ProductUnit
            {
                Id = 1,
                ProductId = 1200,
                UnitId = 102,
                ConversionFactor = 12m
            });
            seedContext.SaveChanges();
        }

        var service = new ProductUnitService(() => CreateContext(connection));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateAsync(1, new ProductUnit
        {
            Id = 1,
            ProductId = 1200,
            UnitId = 102,
            ConversionFactor = factor
        }, [], actorId: 2, Guid.NewGuid()));

        Assert.Contains("greater than zero", exception.Message, StringComparison.OrdinalIgnoreCase);
        using var assertContext = CreateContext(connection);
        Assert.Equal(12m, assertContext.ProductUnits.Single().ConversionFactor);
    }

    [Fact]
    public async Task Add_saves_product_unit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            DatabaseHelper.SeedBasicData(seedContext);
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

        await service.AddAsync(new ProductUnit
        {
            ProductId = 1200,
            UnitId = 102,
            ConversionFactor = 12m,
            IsBaseUnit = false
        }, actorId: 2, Guid.NewGuid());

        using var assertContext = CreateContext(connection);
        var productUnit = Assert.Single(assertContext.ProductUnits);
        Assert.Equal(1200, productUnit.ProductId);
        Assert.Equal(102, productUnit.UnitId);
        Assert.Equal(12m, productUnit.ConversionFactor);
        Assert.False(productUnit.IsBaseUnit);
    }

    [Fact]
    public void Product_can_have_multiple_conversion_units()
    {
        using var connection = CreateDatabase();
        using var context = CreateContext(connection);
        DatabaseHelper.SeedBasicData(context);
        context.Units.AddRange(
            new Unit { Id = 101, UnitCode = "BOX", DisplayName = "Box", IsActive = true },
            new Unit { Id = 102, UnitCode = "PALLET", DisplayName = "Pallet", IsActive = true });
        context.Products.Add(new Product
        {
            Id = 1200,
            ProductCode = "P1200",
            DisplayName = "Multi-unit product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true
        });
        context.ProductUnits.AddRange(
            new ProductUnit { ProductId = 1200, UnitId = 101, ConversionFactor = 12m },
            new ProductUnit { ProductId = 1200, UnitId = 102, ConversionFactor = 120m });

        context.SaveChanges();

        Assert.Equal(2, context.ProductUnits.Count(unit => unit.ProductId == 1200));
    }

    [Fact]
    public void GetByProductId_hydrates_product_and_unit()
    {
        using var connection = CreateDatabase();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Units.Add(
                new Unit { Id = 102, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            seedContext.Products.Add(new Product
            {
                Id = 1200,
                ProductCode = "P1200",
                DisplayName = "Printer",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true
            });
            seedContext.ProductUnits.Add(new ProductUnit
            {
                Id = 30,
                ProductId = 1200,
                UnitId = 102,
                ConversionFactor = 12m
            });
            seedContext.SaveChanges();
        }

        var service = new ProductUnitService(() => CreateContext(connection));

        var productUnit = Assert.Single(service.GetByProductId(1200, includeDefault: false));

        Assert.Equal("Printer", productUnit.Product.DisplayName);
        Assert.Equal("Box", productUnit.Unit.DisplayName);
    }

    [Fact]
    public async Task Add_rejects_unauthorized_actor_without_writing()
    {
        using var connection = CreateDatabase();
        using (var seedContext = CreateContext(connection))
            DatabaseHelper.SeedBasicData(seedContext);
        var service = new ProductUnitService(() => CreateContext(connection));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(
            new ProductUnit
            {
                ProductId = 1200,
                UnitId = 102,
                ConversionFactor = 12m
            },
            actorId: 3,
            Guid.NewGuid()));

        Assert.Contains("not authorized", exception.Message, StringComparison.OrdinalIgnoreCase);
        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.ProductUnits);
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

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        context.Database.EnsureCreated();
        return connection;
    }
}
