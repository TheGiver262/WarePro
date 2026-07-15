using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Tests.Helpers;
using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public sealed class DatabaseSeederProductUnitTests
{
    [Fact]
    public async Task SeedAsync_imports_missing_product_units_without_overwriting_existing_rows()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-product-units-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateWorkbook(workbookPath);
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            int productId;
            int boxUnitId;
            using (var db = DatabaseHelper.CreateContext(connection))
            {
                db.Database.EnsureCreated();
                var category = new Category { CategoryCode = "CAT1", DisplayName = "Category", IsActive = true };
                var brand = new Brand { BrandCode = "BR1", DisplayName = "Brand", IsActive = true };
                var piece = new Unit { UnitCode = "UNIT001", DisplayName = "Cái", IsActive = true };
                var box = new Unit { UnitCode = "UNIT003", DisplayName = "Hộp", IsActive = true };
                db.AddRange(category, brand, piece, box);
                db.SaveChanges();

                var product = new Product
                {
                    ProductCode = "PRD0010",
                    DisplayName = "Keyboard",
                    CategoryId = category.Id,
                    BrandId = brand.Id,
                    DefaultUnitId = piece.Id,
                    DefaultPrice = 100m,
                    IsActive = true
                };
                db.Products.Add(product);
                db.SaveChanges();

                db.ProductUnits.Add(new ProductUnit
                {
                    ProductId = product.Id,
                    UnitId = box.Id,
                    ConversionFactor = 7m,
                    IsPurchaseUnit = true,
                    IsSalesUnit = true
                });
                db.SaveChanges();
                productId = product.Id;
                boxUnitId = box.Id;
            }

            using (var db = DatabaseHelper.CreateContext(connection))
            {
                var seeder = new DatabaseSeeder(db, workbookPath);
                await seeder.SeedAsync();
                await seeder.SeedAsync();
            }

            using (var db = DatabaseHelper.CreateContext(connection))
            {
                var rows = db.ProductUnits.Where(row => row.ProductId == productId).ToList();
                Assert.Equal(2, rows.Count);
                Assert.Equal(7m, rows.Single(row => row.UnitId == boxUnitId).ConversionFactor);
                Assert.Equal(1m, rows.Single(row => row.IsBaseUnit).ConversionFactor);
            }
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    private static void CreateWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Unit",
            ["Id", "UnitCode", "DisplayName", "IsActive"],
            [[1, "UNIT001", "Cái", true], [3, "UNIT003", "Hộp", true]]);
        AddSheet(workbook, "Category",
            ["Id", "CategoryCode", "DisplayName", "IsActive"],
            [[1, "CAT1", "Category", true]]);
        AddSheet(workbook, "Brand",
            ["Id", "BrandCode", "DisplayName", "IsActive"],
            [[1, "BR1", "Brand", true]]);
        AddSheet(workbook, "Supplier",
            ["Id", "SupplierCode", "DisplayName", "IsActive"],
            [[1, "SUP1", "Supplier", true]]);
        AddSheet(workbook, "Customer",
            ["Id", "CustomerCode", "DisplayName", "IsActive"],
            [[1, "CUS1", "Customer", true]]);
        AddSheet(workbook, "Product",
            ["Id", "ProductCode", "DisplayName", "CategoryId", "BrandId", "DefaultUnitId", "DefaultPrice", "IsActive"],
            [[10, "PRD0010", "Keyboard", 1, 1, 1, 100, true]]);
        AddSheet(workbook, "ProductUnit",
            ["Id", "ProductId", "UnitId", "ConversionFactor", "IsBaseUnit", "IsPurchaseUnit", "IsSalesUnit"],
            [[1, 10, 1, 1, true, true, true], [2, 10, 3, 10, false, true, false]]);
        workbook.SaveAs(path);
    }

    private static void AddSheet(XLWorkbook workbook, string name, object[] headers, object[][] rows)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = XLCellValue.FromObject(headers[column]);
        }

        for (var row = 0; row < rows.Length; row++)
        {
            for (var column = 0; column < rows[row].Length; column++)
            {
                sheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(rows[row][column]);
            }
        }
    }
}
