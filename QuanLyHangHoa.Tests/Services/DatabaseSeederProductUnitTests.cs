using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Tests.Helpers;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

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

            var seeder = new DatabaseSeeder(
                () => DatabaseHelper.CreateContext(connection),
                workbookPath);
            await seeder.SeedAsync();
            await seeder.SeedAsync();

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

    [Fact]
    public async Task SeedAsync_parses_malformed_workbook_before_creating_database_context()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-invalid-seed-{Guid.NewGuid():N}.xlsx");
        try
        {
            await File.WriteAllTextAsync(workbookPath, "not an Excel workbook");
            var contextCount = 0;
            var seeder = new DatabaseSeeder(
                () =>
                {
                    contextCount++;
                    throw new InvalidOperationException("database context must not be created");
                },
                workbookPath);

            await Assert.ThrowsAnyAsync<Exception>(() => seeder.SeedAsync());

            Assert.Equal(0, contextCount);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task SeedAsync_uses_fresh_contexts_and_disposes_every_created_context()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-seed-context-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateSparseWorkbook(workbookPath);
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            using (var setup = new AppDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            var createdContexts = new List<TrackingAppDbContext>();
            var disposedCount = 0;
            var seeder = new DatabaseSeeder(
                () =>
                {
                    var context = new TrackingAppDbContext(
                        options,
                        () => Interlocked.Increment(ref disposedCount));
                    createdContexts.Add(context);
                    return context;
                },
                workbookPath);

            await seeder.SeedAsync();
            await seeder.SeedAsync();

            Assert.True(createdContexts.Count >= 4);
            Assert.Equal(createdContexts.Count, disposedCount);
            Assert.Equal(createdContexts.Count, createdContexts.Distinct().Count());
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task SeedAsync_propagates_technical_database_failures()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-seed-failure-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateSparseWorkbook(workbookPath);
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            using (var setup = new AppDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            var seeder = new DatabaseSeeder(
                () => new FailingSaveAppDbContext(options),
                workbookPath);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());

            Assert.Equal("seed write failed", error.Message);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task SeedAsync_accepts_workbook_with_optional_sheets_missing()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-sparse-seed-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateSparseWorkbook(workbookPath);
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using (var setup = DatabaseHelper.CreateContext(connection))
            {
                setup.Database.EnsureCreated();
            }

            var seeder = new DatabaseSeeder(
                () => DatabaseHelper.CreateContext(connection),
                workbookPath);

            await seeder.SeedAsync();

            using var verification = DatabaseHelper.CreateContext(connection);
            Assert.Single(verification.Units);
            Assert.Single(verification.Warehouses);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task SeedAsync_rejects_invalid_product_unit_before_creating_database_context()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-invalid-product-unit-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                AddSheet(workbook, "ProductUnit",
                    ["ProductId", "UnitId", "ConversionFactor"],
                    [[10, 1, 0]]);
                workbook.SaveAs(workbookPath);
            }

            var contextCount = 0;
            var seeder = new DatabaseSeeder(
                () =>
                {
                    contextCount++;
                    throw new InvalidOperationException("database context must not be created");
                },
                workbookPath);

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => seeder.SeedAsync());

            Assert.Contains("ConversionFactor", error.Message, StringComparison.Ordinal);
            Assert.Equal(0, contextCount);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task SeedAsync_rejects_missing_product_unit_reference_before_creating_database_context()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-missing-product-unit-ref-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                AddSheet(workbook, "ProductUnit",
                    ["ProductId", "UnitId", "ConversionFactor"],
                    [["", 1, 1]]);
                workbook.SaveAs(workbookPath);
            }

            var contextCount = 0;
            var seeder = new DatabaseSeeder(
                () =>
                {
                    contextCount++;
                    throw new InvalidOperationException("database context must not be created");
                },
                workbookPath);

            var error = await Assert.ThrowsAsync<InvalidDataException>(() => seeder.SeedAsync());

            Assert.Contains("ProductId", error.Message, StringComparison.Ordinal);
            Assert.Equal(0, contextCount);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public void Seeder_executor_receives_only_prepared_scalar_rows()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "QuanLyHangHoa",
            "Services",
            "DataImport",
            "DatabaseSeeder.cs"));

        Assert.DoesNotMatch(
            new Regex(@"SeedWorkbookAsync\s*\(\s*XLWorkbook", RegexOptions.CultureInvariant),
            source);
        Assert.DoesNotContain("SeedTableWithMappingAsync<T>(XLWorkbook", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SeedProductUnitsAsync(XLWorkbook", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedAsync_threads_cancellation_into_internal_ef_saves()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"warepro-cancel-seed-{Guid.NewGuid():N}.xlsx");
        try
        {
            CreateSparseWorkbook(workbookPath);
            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            using (var setup = new AppDbContext(options))
            {
                setup.Database.EnsureCreated();
            }

            using var cancellationSource = new CancellationTokenSource();
            var seeder = new DatabaseSeeder(
                () => new CancellingSaveAppDbContext(options, cancellationSource),
                workbookPath);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => seeder.SeedAsync(cancellationSource.Token));
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

    private static void CreateSparseWorkbook(string path)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Unit",
            ["Id", "UnitCode", "DisplayName", "IsActive"],
            [[1, "UNIT001", "Cái", true]]);
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

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !Directory.Exists(Path.Combine(directory.FullName, "QuanLyHangHoa")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc của repo.");
    }

    private sealed class TrackingAppDbContext(
        DbContextOptions<AppDbContext> options,
        Action onDispose) : AppDbContext(options)
    {
        private int _disposed;

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                onDispose();
            }

            await base.DisposeAsync();
        }
    }

    private sealed class FailingSaveAppDbContext(
        DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("seed write failed");
    }

    private sealed class CancellingSaveAppDbContext(
        DbContextOptions<AppDbContext> options,
        CancellationTokenSource cancellationSource) : AppDbContext(options)
    {
        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                throw new InvalidOperationException("seed did not pass its cancellation token");
            }

            cancellationSource.Cancel();
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
