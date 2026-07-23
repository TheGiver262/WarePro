using System.IO;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class ImportWriteSafetyTests
{
    [Fact]
    public async Task Product_serial_import_replaying_same_operation_creates_one_batch()
    {
        using var connection = CreateDatabase();
        var path = CreateSerialWorkbook();
        var operationId = Guid.NewGuid();

        try
        {
            var service = new ProductSerialImportService(() => DatabaseHelper.CreateContext(connection));

            var first = await service.ImportFromExcelAsync(path, 1, operationId);
            var replay = await service.ImportFromExcelAsync(path, 1, operationId);

            Assert.Equal(1, first.SuccessCount);
            Assert.Equal(1, replay.SuccessCount);
            using var db = DatabaseHelper.CreateContext(connection);
            Assert.Single(db.StockIns);
            Assert.Single(db.StockInLines);
            Assert.Single(db.ProductSerials);
            var balance = Assert.Single(db.StockBalances);
            Assert.Equal(1501, balance.ProductId);
            Assert.Equal(1m, balance.OnHandQuantity);
            var ledger = Assert.Single(db.StockLedgers);
            Assert.Equal(1501, ledger.ProductId);
            Assert.Equal(1m, ledger.Quantity);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Product_serial_import_rejects_same_operation_with_different_payload()
    {
        using var connection = CreateDatabase();
        var firstPath = CreateSerialWorkbook("SER-IMPORT-001");
        var replayPath = CreateSerialWorkbook("SER-IMPORT-002");
        var operationId = Guid.NewGuid();

        try
        {
            var service = new ProductSerialImportService(() => DatabaseHelper.CreateContext(connection));

            var first = await service.ImportFromExcelAsync(firstPath, 1, operationId);
            var replay = await service.ImportFromExcelAsync(replayPath, 1, operationId);

            Assert.Equal(1, first.SuccessCount);
            Assert.Equal(0, replay.SuccessCount);
            Assert.Contains("different import payload", replay.Message, StringComparison.Ordinal);
            using var db = DatabaseHelper.CreateContext(connection);
            Assert.Single(db.StockIns);
            Assert.Single(db.ProductSerials);
            Assert.Equal("SER-IMPORT-001", db.ProductSerials.Single().SerialNumber);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(replayPath);
        }
    }
    [Fact]
    public async Task Product_serial_import_honors_pre_cancelled_token_without_writes()
    {
        using var connection = CreateDatabase();
        var path = CreateSerialWorkbook();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            var service = new ProductSerialImportService(() => DatabaseHelper.CreateContext(connection));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.ImportFromExcelAsync(path, 1, Guid.NewGuid(), cancellation.Token));

            using var db = DatabaseHelper.CreateContext(connection);
            Assert.Empty(db.StockIns);
            Assert.Empty(db.ProductSerials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Opening_balance_replaying_same_operation_returns_original_row_count()
    {
        using var connection = CreateDatabase();
        var operationId = Guid.NewGuid();
        var rows = NewOpeningRows();
        var service = new OpeningBalanceImportService(() => DatabaseHelper.CreateContext(connection));

        var first = await service.ImportRowsAsync(rows, 1, operationId);
        var replay = await service.ImportRowsAsync(rows, 1, operationId);

        Assert.Equal(2, first.SuccessCount);
        Assert.Equal(2, replay.SuccessCount);
        Assert.Empty(first.Errors);
        Assert.Empty(replay.Errors);
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Single(db.StockIns);
        Assert.Equal(2, db.StockInLines.Count());
        Assert.Equal(2, db.StockBalances.Count());
        Assert.Equal(2, db.StockLedgers.Count());
        Assert.Equal(2, db.ProductSerials.Count());
    }

    [Fact]
    public async Task Opening_balance_rejects_same_operation_with_different_payload()
    {
        using var connection = CreateDatabase();
        var operationId = Guid.NewGuid();
        var firstRows = NewOpeningRows();
        var changedRows = NewOpeningRows();
        changedRows[0].Quantity = 4;
        var service = new OpeningBalanceImportService(() => DatabaseHelper.CreateContext(connection));

        var first = await service.ImportRowsAsync(firstRows, 1, operationId);
        var replay = await service.ImportRowsAsync(changedRows, 1, operationId);

        Assert.Equal(2, first.SuccessCount);
        Assert.Equal(0, replay.SuccessCount);
        Assert.Contains(
            replay.Errors,
            error => error.ErrorMessage.Contains("different import payload", StringComparison.Ordinal));
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Single(db.StockIns);
        Assert.Equal(2, db.StockInLines.Count());
        Assert.Equal(3m, db.StockInLines.Single(line => line.ProductId == 1500).Quantity);
        Assert.Equal(2, db.StockBalances.Count());
        Assert.Equal(2, db.StockLedgers.Count());
    }
    [Fact]
    public async Task Opening_balance_honors_pre_cancelled_token_without_writes()
    {
        using var connection = CreateDatabase();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new OpeningBalanceImportService(() => DatabaseHelper.CreateContext(connection));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ImportRowsAsync(NewOpeningRows(), 1, Guid.NewGuid(), cancellation.Token));

        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Empty(db.StockIns);
        Assert.Empty(db.StockBalances);
        Assert.Empty(db.StockLedgers);
        Assert.Empty(db.ProductSerials);
    }

    [Fact]
    public async Task Opening_balance_rolls_back_document_inventory_and_audit_on_late_failure()
    {
        using var connection = CreateDatabase();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailOpeningAudit
                BEFORE INSERT ON AuditLog
                BEGIN
                    SELECT RAISE(ABORT, 'forced opening audit failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var service = new OpeningBalanceImportService(() => DatabaseHelper.CreateContext(connection));

        var result = await service.ImportRowsAsync(NewOpeningRows(), 1, Guid.NewGuid());

        Assert.Equal(0, result.SuccessCount);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage.Contains("forced opening audit failure", StringComparison.Ordinal));
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Empty(db.StockIns);
        Assert.Empty(db.StockInLines);
        Assert.Empty(db.StockBalances);
        Assert.Empty(db.StockLedgers);
        Assert.Empty(db.ProductSerials);
        Assert.Empty(db.AuditLogs);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Warehouses.Single(item => item.Id == 1).IsDefault = true;
        db.Products.AddRange(
            new Product
            {
                Id = 1500,
                ProductCode = "P1500",
                DisplayName = "Opening non serial",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true,
                IsSerialTracked = false
            },
            new Product
            {
                Id = 1501,
                ProductCode = "P1501",
                DisplayName = "Opening serial",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 20m,
                IsActive = true,
                IsSerialTracked = true
            });
        db.SaveChanges();
        return connection;
    }

    private static OpeningBalanceImportRow[] NewOpeningRows() =>
    [
        new()
        {
            RowNumber = 2,
            ProductId = 1500,
            Quantity = 3,
            SerialNumbers = string.Empty
        },
        new()
        {
            RowNumber = 3,
            ProductId = 1501,
            Quantity = 2,
            SerialNumbers = "SER-OPEN-001,SER-OPEN-002"
        }
    ];

    private static string CreateSerialWorkbook(string serialNumber = "SER-IMPORT-001")
    {
        var path = Path.Combine(Path.GetTempPath(), $"serial-safe-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var products = workbook.Worksheets.Add("Sản phẩm");
        products.Cell(1, 1).Value = "id";
        products.Cell(1, 2).Value = "ProductCode";
        products.Cell(2, 1).Value = "mongo-1501";
        products.Cell(2, 2).Value = "P1501";
        var serials = workbook.Worksheets.Add("Serial");
        serials.Cell(1, 1).Value = "SerialCode";
        serials.Cell(1, 2).Value = "ProductId";
        serials.Cell(2, 1).Value = serialNumber;
        serials.Cell(2, 2).Value = "mongo-1501";
        workbook.SaveAs(path);
        return path;
    }
}
