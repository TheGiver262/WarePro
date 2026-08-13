using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public sealed class StockDocumentDraftValidatorTests
{
    [Fact]
    public async Task ValidateAsync_accepts_serial_count_equal_to_converted_base_quantity()
    {
        using var connection = CreateDatabase(serialTracked: true, includeMapping: true);
        using var db = DatabaseHelper.CreateContext(connection);
        var line = new StockInLine
        {
            ProductId = 900,
            UnitId = 2,
            Quantity = 2m,
            DraftSerials = string.Join(',', Enumerable.Range(1, 20).Select(i => $"SN-{i:00}"))
        };

        await StockDocumentDraftValidator.ValidateAsync(db, [line], CancellationToken.None);

        Assert.Equal(20m, line.BaseQuantity);
    }

    [Theory]
    [InlineData(2, "SN-01", true, "serial")]
    [InlineData(0.15, "SN-01,SN-02", true, "nguyên")]
    [InlineData(1, "SN-01,sn-01,SN-02,SN-03,SN-04,SN-05,SN-06,SN-07,SN-08,SN-09", true, "trùng")]
    [InlineData(1, "SN-01", false, "không quản lý serial")]
    public async Task ValidateAsync_rejects_invalid_serial_invariants(
        double quantity,
        string serials,
        bool serialTracked,
        string expectedMessage)
    {
        using var connection = CreateDatabase(serialTracked, includeMapping: true);
        using var db = DatabaseHelper.CreateContext(connection);
        var line = new StockOutLine
        {
            ProductId = 900,
            UnitId = 2,
            Quantity = (decimal)quantity,
            DraftSerials = serials
        };

        var error = await Assert.ThrowsAsync<InventoryDomainException>(() =>
            StockDocumentDraftValidator.ValidateAsync(db, [line], CancellationToken.None));

        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_rejects_missing_product_unit_mapping()
    {
        using var connection = CreateDatabase(serialTracked: false, includeMapping: false);
        using var db = DatabaseHelper.CreateContext(connection);
        var line = new StockInLine { ProductId = 900, UnitId = 2, Quantity = 1m };

        var error = await Assert.ThrowsAsync<InventoryDomainException>(() =>
            StockDocumentDraftValidator.ValidateAsync(db, [line], CancellationToken.None));

        Assert.Contains("đơn vị", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SqliteConnection CreateDatabase(bool serialTracked, bool includeMapping)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
        db.Products.Add(new Product
        {
            Id = 900,
            ProductCode = "P900",
            DisplayName = "Draft validator product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 1m,
            IsSerialTracked = serialTracked
        });
        if (includeMapping)
        {
            db.ProductUnits.Add(new ProductUnit
            {
                ProductId = 900,
                UnitId = 2,
                ConversionFactor = 10m,
                IsPurchaseUnit = true,
                IsSalesUnit = true
            });
        }
        db.SaveChanges();
        return connection;
    }
}
