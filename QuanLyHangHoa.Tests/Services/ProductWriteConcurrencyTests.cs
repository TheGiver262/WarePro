using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class ProductWriteConcurrencyTests
{
    [Fact]
    public async Task Product_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        Product stale;
        using (var db = CreateContext(connection))
            stale = db.Products.AsNoTracking().Single(item => item.Id == 1400);
        Overwrite(connection, db => db.Products.Single(item => item.Id == 1400).DisplayName = "Concurrent product");

        var service = new ProductService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateProductAsync(
            stale.Id,
            new Product
            {
                ProductCode = stale.ProductCode,
                DisplayName = "Stale product",
                CategoryId = stale.CategoryId,
                BrandId = stale.BrandId,
                DefaultUnitId = stale.DefaultUnitId,
                DefaultPrice = stale.DefaultPrice,
                OriginCountry = stale.OriginCountry,
                WarrantyPeriodMonths = stale.WarrantyPeriodMonths,
                IsSerialTracked = stale.IsSerialTracked,
                IsActive = stale.IsActive
            },
            stale.RowVersion,
            userId: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent product", verify.Products.Single(item => item.Id == 1400).DisplayName);
    }

    [Fact]
    public async Task Product_update_fails_when_entity_was_deleted_after_client_read()
    {
        using var connection = CreateDatabase();
        Product stale;
        using (var db = CreateContext(connection))
            stale = db.Products.AsNoTracking().Single(item => item.Id == 1400);

        // Client B xóa entity sau khi Client A đã đọc
        using (var db = CreateContext(connection))
        {
            var toDelete = db.Products.Single(item => item.Id == 1400);
            db.Products.Remove(toDelete);
            db.SaveChanges();
        }

        var service = new ProductService(() => CreateContext(connection));

        // Client A gọi Update với dữ liệu cũ — phải fail rõ ràng, không silent success
        await Assert.ThrowsAsync<QuanLyHangHoa.Inventory.InventoryDomainException>(() => service.UpdateProductAsync(
            stale.Id,
            new Product
            {
                ProductCode = stale.ProductCode,
                DisplayName = "Stale update after delete",
                CategoryId = stale.CategoryId,
                BrandId = stale.BrandId,
                DefaultUnitId = stale.DefaultUnitId,
                DefaultPrice = stale.DefaultPrice,
                OriginCountry = stale.OriginCountry,
                WarrantyPeriodMonths = stale.WarrantyPeriodMonths,
                IsSerialTracked = stale.IsSerialTracked,
                IsActive = stale.IsActive
            },
            stale.RowVersion,
            userId: 1,
            Guid.NewGuid()));

        // Assert: không có product nào bị tạo lại
        using var verify = CreateContext(connection);
        Assert.False(verify.Products.Any(item => item.Id == 1400));
    }

    [Fact]
    public async Task Product_unit_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        ProductUnit stale;
        using (var db = CreateContext(connection))
            stale = db.ProductUnits.AsNoTracking().Single(item => item.Id == 50);
        Overwrite(connection, db => db.ProductUnits.Single(item => item.Id == 50).ConversionFactor = 24m);

        var service = new ProductUnitService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateAsync(
            stale.Id,
            new ProductUnit
            {
                ProductId = stale.ProductId,
                UnitId = stale.UnitId,
                ConversionFactor = 6m,
                IsBaseUnit = stale.IsBaseUnit,
                IsPurchaseUnit = stale.IsPurchaseUnit,
                IsSalesUnit = stale.IsSalesUnit
            },
            stale.RowVersion,
            actorId: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal(24m, verify.ProductUnits.Single(item => item.Id == 50).ConversionFactor);
    }

    [Fact]
    public async Task Serial_note_update_rejects_stale_rowversion()
    {
        using var connection = CreateDatabase();
        ProductSerial stale;
        using (var db = CreateContext(connection))
            stale = db.ProductSerials.AsNoTracking().Single(item => item.Id == 60);
        Overwrite(connection, db => db.ProductSerials.Single(item => item.Id == 60).Note = "Concurrent note");

        var service = new ProductSerialService(() => CreateContext(connection));
        await Assert.ThrowsAsync<DatabaseWriteConflictException>(() => service.UpdateNoteAsync(
            stale.Id,
            "Stale note",
            stale.RowVersion,
            userId: 1,
            Guid.NewGuid()));

        using var verify = CreateContext(connection);
        Assert.Equal("Concurrent note", verify.ProductSerials.Single(item => item.Id == 60).Note);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 1400,
            ProductCode = "P1400",
            DisplayName = "Product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true
        });
        db.ProductUnits.Add(new ProductUnit
        {
            Id = 50,
            ProductId = 1400,
            UnitId = 2,
            ConversionFactor = 12m,
            IsPurchaseUnit = true,
            IsSalesUnit = true
        });
        db.ProductSerials.Add(new ProductSerial
        {
            Id = 60,
            ProductId = 1400,
            SerialNumber = "SER-1400",
            CurrentStatus = "InStock",
            CurrentWarehouseId = 1,
            Note = "Original note"
        });
        db.SaveChanges();
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
