using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class ProductSerialServiceTests
{
    [Fact]
    public void SearchSerials_returns_matching_serial_with_product_and_warehouse()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedSerials(seedContext);
        }

        var service = new ProductSerialService(() => CreateContext(connection));

        var serials = service.SearchSerials("ABC", "All");

        var serial = Assert.Single(serials);
        Assert.Equal("ABC-001", serial.SerialNumber);
        Assert.Equal("Serial product", serial.Product?.Name);
        Assert.Equal("Main warehouse", serial.CurrentWarehouse?.Name);
    }

    [Fact]
    public void SearchSerials_filters_by_status_and_ignores_deleted_serials()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            SeedSerials(seedContext);
        }

        var service = new ProductSerialService(() => CreateContext(connection));

        var serials = service.SearchSerials(string.Empty, "Sold");

        var serial = Assert.Single(serials);
        Assert.Equal("SOLD-001", serial.SerialNumber);
        Assert.Equal("Sold", serial.Status);
    }

    private static void SeedSerials(AppDbContext context)
    {
        context.Products.Add(new Product
        {
            Id = 1300,
            Name = "Serial product",
            CategoryId = 1,
            BrandId = 1,
            UnitId = 1,
            Quantity = 0,
            UnitPrice = 10m,
            IsSerialManaged = true
        });
        context.ProductSerials.AddRange(
            new ProductSerial
            {
                ProductId = 1300,
                SerialNumber = "ABC-001",
                Status = "InStock",
                CurrentWarehouseId = 1
            },
            new ProductSerial
            {
                ProductId = 1300,
                SerialNumber = "SOLD-001",
                Status = "Sold",
                CurrentWarehouseId = null
            },
            new ProductSerial
            {
                ProductId = 1300,
                SerialNumber = "DELETED-001",
                Status = "Sold",
                CurrentWarehouseId = null,
                IsDeleted = true
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
