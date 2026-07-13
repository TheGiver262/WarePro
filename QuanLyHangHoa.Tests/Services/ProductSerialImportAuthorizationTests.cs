using ClosedXML.Excel;
using System.IO;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class ProductSerialImportAuthorizationTests
{
    [Fact]
    public async Task Import_uses_explicit_actor_for_created_and_posted_by()
    {
        using var connection = CreateDatabase();
        var path = CreateWorkbook();
        try
        {
            var service = new ProductSerialImportService(() => CreateContext(connection));

            var result = await service.ImportFromExcelAsync(path, actorId: 2);

            Assert.Equal(1, result.SuccessCount);
            using var db = CreateContext(connection);
            var stockIn = Assert.Single(db.StockIns);
            Assert.Equal(2, stockIn.CreatedBy);
            Assert.Equal(2, stockIn.PostedBy);
            Assert.DoesNotContain(db.StockIns, item => item.CreatedBy == 1 || item.PostedBy == 1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_rejects_inactive_actor_without_writes()
    {
        using var connection = CreateDatabase();
        using (var db = CreateContext(connection))
        {
            db.AppUsers.Single(user => user.Id == 2).IsActive = false;
            db.SaveChanges();
        }
        var path = CreateWorkbook();
        try
        {
            var service = new ProductSerialImportService(() => CreateContext(connection));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ImportFromExcelAsync(path, actorId: 2));

            Assert.Equal("The current user is not authorized for this action.", error.Message);
            using var db = CreateContext(connection);
            Assert.Empty(db.StockIns);
            Assert.Empty(db.ProductSerials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Warehouses.Single(item => item.Id == 1).IsDefault = true;
        db.Products.Add(new Product
        {
            Id = 100,
            ProductCode = "P100",
            DisplayName = "Serial product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true,
            IsSerialTracked = true
        });
        db.SaveChanges();
        return connection;
    }

    private static string CreateWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"serial-auth-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var products = workbook.Worksheets.Add("S\u1ea3n ph\u1ea9m");
        products.Cell(1, 1).Value = "id";
        products.Cell(1, 2).Value = "ProductCode";
        products.Cell(2, 1).Value = "mongo-100";
        products.Cell(2, 2).Value = "P100";
        var serials = workbook.Worksheets.Add("Serial");
        serials.Cell(1, 1).Value = "SerialCode";
        serials.Cell(1, 2).Value = "ProductId";
        serials.Cell(2, 1).Value = "SN-AUTH-001";
        serials.Cell(2, 2).Value = "mongo-100";
        workbook.SaveAs(path);
        return path;
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
