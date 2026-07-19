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

public class MasterDataIntegrityTests
{
    [Theory]
    [InlineData("ProductUnit")]
    [InlineData("ProductSerial")]
    [InlineData("StockBalance")]
    [InlineData("StockTransferLine")]
    public void Product_dependencies_include_each_mapped_reference(string dependencyName)
    {
        using var connection = CreateDatabase();
        SeedProductDependency(connection, dependencyName);
        var service = new ProductService(() => CreateContext(connection));

        var dependency = Assert.Single(service.GetDependencies(100), item => item.Name == dependencyName);

        Assert.Equal(1, dependency.Count);
    }

    [Theory]
    [InlineData("ProductUnit")]
    [InlineData("StockTransferLine")]
    public void Unit_dependencies_include_missing_mapped_reference(string dependencyName)
    {
        using var connection = CreateDatabase();
        using (var db = CreateContext(connection))
        {
            if (dependencyName == "ProductUnit")
            {
                db.ProductUnits.Add(new ProductUnit { ProductId = 100, UnitId = 10, ConversionFactor = 1m });
            }
            else
            {
                db.StockTransferLines.Add(new StockTransferLine
                {
                    StockTransferId = 1,
                    ProductId = 100,
                    UnitId = 10,
                    Quantity = 1m,
                    BaseQuantity = 1m
                });
            }
            db.SaveChanges();
        }

        var service = new UnitService(() => CreateContext(connection));
        var dependency = Assert.Single(service.GetDependencies(10), item => item.Name == dependencyName);

        Assert.Equal(1, dependency.Count);
    }

    [Theory]
    [InlineData("ProductUnit")]
    [InlineData("ProductSerial")]
    [InlineData("StockBalance")]
    [InlineData("StockTransferLine")]
    public async Task DeleteProduct_deactivates_product_with_dependency(string dependencyName)
    {
        using var connection = CreateDatabase();
        SeedProduct(connection, isActive: true);
        SeedProductDependency(connection, dependencyName);
        byte[] rowVersion;
        using (var db = CreateContext(connection))
            rowVersion = db.Products.AsNoTracking().Single(item => item.Id == 100).RowVersion;
        var service = new ProductService(() => CreateContext(connection));

        await service.DeleteProductAsync(100, rowVersion, userId: 1, Guid.NewGuid());

        using var assertContext = CreateContext(connection);
        Assert.False(assertContext.Products.Single(product => product.Id == 100).IsActive);
        Assert.Equal("DEACTIVATE", assertContext.AuditLogs.Single().ActionCode);
    }

    [Fact]
    public async Task DeleteUnit_deactivates_unit_with_dependency()
    {
        using var connection = CreateDatabase();
        using (var db = CreateContext(connection))
        {
            db.Units.Add(new Unit { Id = 10, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            db.ProductUnits.Add(new ProductUnit { ProductId = 100, UnitId = 10, ConversionFactor = 1m });
            db.SaveChanges();
        }

        byte[] rowVersion;
        using (var db = CreateContext(connection))
            rowVersion = db.Units.AsNoTracking().Single(unit => unit.Id == 10).RowVersion;
        var service = new UnitService(() => CreateContext(connection));
        await service.DeleteAsync(10, rowVersion, performedBy: 1, Guid.NewGuid());

        using var assertContext = CreateContext(connection);
        Assert.False(assertContext.Units.Single(unit => unit.Id == 10).IsActive);
        Assert.Equal("DEACTIVATE", assertContext.AuditLogs.Single().ActionCode);
    }

    [Fact]
    public async Task Create_rolls_back_when_audit_insert_fails()
    {
        using var connection = CreateDatabaseWithFailingAudit();
        var service = new ProductService(() => CreateContext(connection));

        await Assert.ThrowsAnyAsync<Exception>(() => service.AddProductAsync(NewProduct(100), userId: 1, Guid.NewGuid()));

        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.Products);
    }

    [Fact]
    public async Task Update_rolls_back_when_audit_insert_fails()
    {
        using var connection = CreateDatabase();
        using (var db = CreateContext(connection))
        {
            db.Categories.Add(new Category { Id = 20, CategoryCode = "OLD", DisplayName = "Old", IsActive = true });
            db.SaveChanges();
        }
        InstallFailingAuditTrigger(connection);
        var service = new CategoryService(() => CreateContext(connection));

        byte[] rowVersion;
        using (var db = CreateContext(connection))
            rowVersion = db.Categories.AsNoTracking().Single(item => item.Id == 20).RowVersion;

        await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateAsync(
            20,
            new Category { Id = 20, CategoryCode = "NEW", DisplayName = "New", IsActive = true },
            rowVersion,
            performedBy: 1,
            Guid.NewGuid()));

        using var assertContext = CreateContext(connection);
        Assert.Equal("OLD", assertContext.Categories.Single().CategoryCode);
    }

    [Fact]
    public async Task Deactivate_rolls_back_when_audit_insert_fails()
    {
        using var connection = CreateDatabase();
        SeedProduct(connection, isActive: true);
        InstallFailingAuditTrigger(connection);
        byte[] rowVersion;
        using (var db = CreateContext(connection))
            rowVersion = db.Products.AsNoTracking().Single(item => item.Id == 100).RowVersion;
        var service = new ProductService(() => CreateContext(connection));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.DeactivateProductAsync(100, rowVersion, userId: 1, Guid.NewGuid()));

        using var assertContext = CreateContext(connection);
        Assert.True(assertContext.Products.Single().IsActive);
    }

    [Fact]
    public async Task Delete_rolls_back_when_audit_insert_fails()
    {
        using var connection = CreateDatabase();
        using (var db = CreateContext(connection))
        {
            db.Units.Add(new Unit { Id = 10, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            db.SaveChanges();
        }
        InstallFailingAuditTrigger(connection);
        byte[] rowVersion;
        using (var db = CreateContext(connection))
            rowVersion = db.Units.AsNoTracking().Single(unit => unit.Id == 10).RowVersion;
        var service = new UnitService(() => CreateContext(connection));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.DeleteAsync(10, rowVersion, performedBy: 1, Guid.NewGuid()));

        using var assertContext = CreateContext(connection);
        Assert.Equal("BOX", assertContext.Units.Single().UnitCode);
    }

    [Fact]
    public void Database_rejects_non_positive_conversion_factor()
    {
        using var connection = CreateDatabase();
        using var db = CreateContext(connection);
        db.ProductUnits.Add(new ProductUnit { ProductId = 100, UnitId = 10, ConversionFactor = 0m });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    private static void SeedProductDependency(SqliteConnection connection, string dependencyName)
    {
        using var db = CreateContext(connection);
        switch (dependencyName)
        {
            case "ProductUnit":
                db.ProductUnits.Add(new ProductUnit { ProductId = 100, UnitId = 10, ConversionFactor = 1m });
                break;
            case "ProductSerial":
                db.ProductSerials.Add(new ProductSerial
                {
                    ProductId = 100,
                    SerialNumber = "SER-100",
                    CurrentStatus = "InStock",
                    LastStockInLineId = 1
                });
                break;
            case "StockBalance":
                db.StockBalances.Add(new StockBalance { ProductId = 100, WarehouseId = 1 });
                break;
            case "StockTransferLine":
                db.StockTransferLines.Add(new StockTransferLine
                {
                    StockTransferId = 1,
                    ProductId = 100,
                    UnitId = 10,
                    Quantity = 1m,
                    BaseQuantity = 1m
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dependencyName));
        }
        db.SaveChanges();
    }

    private static void SeedProduct(SqliteConnection connection, bool isActive)
    {
        using var db = CreateContext(connection);
        db.Products.Add(NewProduct(100, isActive));
        db.SaveChanges();
    }

    private static Product NewProduct(int id, bool isActive = true) => new()
    {
        Id = id,
        ProductCode = $"P{id}",
        DisplayName = $"Product {id}",
        CategoryId = 1,
        BrandId = 1,
        DefaultUnitId = 10,
        DefaultPrice = 1m,
        IsActive = isActive
    };

    private static SqliteConnection CreateDatabaseWithFailingAudit()
    {
        var connection = CreateDatabase();
        InstallFailingAuditTrigger(connection);
        return connection;
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        db.Database.EnsureCreated();
        db.AppUsers.Add(new AppUser
        {
            Id = 1,
            Username = "admin",
            FullName = "Administrator",
            PasswordHash = "hash",
            RoleCode = "Quản trị viên",
            IsActive = true
        });
        db.SaveChanges();
        return connection;
    }

    private static void InstallFailingAuditTrigger(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER FailAuditInsert
            BEFORE INSERT ON AuditLog
            BEGIN
                SELECT RAISE(FAIL, 'forced audit failure');
            END;
            """;
        command.ExecuteNonQuery();
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
