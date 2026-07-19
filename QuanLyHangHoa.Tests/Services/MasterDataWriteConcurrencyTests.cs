using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class MasterDataWriteConcurrencyTests
{
    [Fact]
    public async Task Category_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        Category stale;
        using (var db = CreateContext(connection))
            stale = db.Categories.AsNoTracking().Single(item => item.Id == 1);
        Overwrite(connection, db => db.Categories.Single(item => item.Id == 1).DisplayName = "Concurrent category");

        var service = new CategoryService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateAsync(
            stale.Id,
            new Category { CategoryCode = stale.CategoryCode, DisplayName = "Stale category", IsActive = true },
            stale.RowVersion,
            performedBy: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent category", verify.Categories.Single(item => item.Id == 1).DisplayName);
    }

    [Fact]
    public async Task Brand_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        Brand stale;
        using (var db = CreateContext(connection))
            stale = db.Brands.AsNoTracking().Single(item => item.Id == 1);
        Overwrite(connection, db => db.Brands.Single(item => item.Id == 1).DisplayName = "Concurrent brand");

        var service = new BrandService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateAsync(
            stale.Id,
            new Brand
            {
                BrandCode = stale.BrandCode,
                DisplayName = "Stale brand",
                OriginCountry = stale.OriginCountry,
                IsActive = true
            },
            stale.RowVersion,
            performedBy: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent brand", verify.Brands.Single(item => item.Id == 1).DisplayName);
    }

    [Fact]
    public async Task Customer_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        Customer stale;
        using (var db = CreateContext(connection))
            stale = db.Customers.AsNoTracking().Single(item => item.Id == 1);
        Overwrite(connection, db => db.Customers.Single(item => item.Id == 1).DisplayName = "Concurrent customer");

        var service = new CustomerService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateAsync(
            stale.Id,
            new Customer
            {
                CustomerCode = stale.CustomerCode,
                DisplayName = "Stale customer",
                Phone = stale.Phone,
                Email = stale.Email,
                Address = stale.Address,
                IsActive = true
            },
            stale.RowVersion,
            performedBy: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent customer", verify.Customers.Single(item => item.Id == 1).DisplayName);
    }

    [Fact]
    public async Task Supplier_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        Supplier stale;
        using (var db = CreateContext(connection))
            stale = db.Suppliers.AsNoTracking().Single(item => item.Id == 1);
        Overwrite(connection, db => db.Suppliers.Single(item => item.Id == 1).DisplayName = "Concurrent supplier");

        var service = new SupplierService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateAsync(
            stale.Id,
            new Supplier
            {
                SupplierCode = stale.SupplierCode,
                DisplayName = "Stale supplier",
                Phone = stale.Phone,
                Email = stale.Email,
                Address = stale.Address,
                IsActive = true
            },
            stale.RowVersion,
            performedBy: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent supplier", verify.Suppliers.Single(item => item.Id == 1).DisplayName);
    }

    [Fact]
    public async Task Unit_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        Unit stale;
        using (var db = CreateContext(connection))
            stale = db.Units.AsNoTracking().Single(item => item.Id == 1);
        Overwrite(connection, db => db.Units.Single(item => item.Id == 1).DisplayName = "Concurrent unit");

        var service = new UnitService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateAsync(
            stale.Id,
            new Unit { UnitCode = stale.UnitCode, DisplayName = "Stale unit", IsActive = true },
            stale.RowVersion,
            performedBy: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent unit", verify.Units.Single(item => item.Id == 1).DisplayName);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);

        return connection;
    }

    private static void Overwrite(SqliteConnection connection, Action<AppDbContext> change)
    {
        using var db = CreateContext(connection);
        change(db);
        db.SaveChanges();
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
