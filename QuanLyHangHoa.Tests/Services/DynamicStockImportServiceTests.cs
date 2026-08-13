using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class DynamicStockImportServiceTests
{
    [Fact]
    public void Product_serial_import_posts_inventory_and_uses_its_real_stock_in_line()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.Add(CreateProduct(1699, "IMPORT-DYNAMIC-SERIAL", serialTracked: true));
            db.SaveChanges();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var result = service.ExecuteImport(
            new List<Dictionary<string, string>>
            {
                new(StringComparer.OrdinalIgnoreCase)
                {
                    ["SerialNumber"] = "DYNAMIC-SERIAL-001",
                    ["ProductCode"] = "IMPORT-DYNAMIC-SERIAL",
                    ["WarehouseName"] = string.Empty,
                    ["Note"] = "opening serial"
                }
            },
            ImportFileType.ProductSerial,
            IdentityMappings("SerialNumber", "ProductCode", "WarehouseName", "Note"),
            userId: 1,
            autoCreateReferences: false);

        Assert.Equal(1, result.SuccessCount);
        Assert.Empty(result.Errors);
        using var assertion = DatabaseHelper.CreateContext(connection);
        var serial = assertion.ProductSerials
            .Include(item => item.LastStockInLine)
            .ThenInclude(line => line.StockIn)
            .Single(item => item.SerialNumber == "DYNAMIC-SERIAL-001");
        var stockInLine = Assert.IsType<StockInLine>(serial.LastStockInLine);
        var stockIn = Assert.IsType<StockIn>(stockInLine.StockIn);
        Assert.Equal(1699, stockInLine.ProductId);
        Assert.Equal("OpeningBalance", stockIn.PurposeCode);
        var balance = assertion.StockBalances.Single(item =>
            item.ProductId == 1699 && item.WarehouseId == serial.CurrentWarehouseId);
        Assert.Equal(1m, balance.OnHandQuantity);
        var ledger = assertion.StockLedgers.Single(item =>
            item.ProductId == 1699 && item.WarehouseId == serial.CurrentWarehouseId);
        Assert.Equal(1m, ledger.Quantity);
    }

    [Fact]
    public async Task Product_serial_import_replay_does_not_double_inventory()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.Add(CreateProduct(1698, "IMPORT-DYNAMIC-REPLAY", serialTracked: true));
            db.SaveChanges();
        }

        var rows = new List<Dictionary<string, string>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["SerialNumber"] = "DYNAMIC-REPLAY-001",
                ["ProductCode"] = "IMPORT-DYNAMIC-REPLAY",
                ["WarehouseName"] = string.Empty,
                ["Note"] = string.Empty
            }
        };
        var mappings = IdentityMappings("SerialNumber", "ProductCode", "WarehouseName", "Note");
        var operationId = Guid.NewGuid();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        var first = await service.ExecuteImportAsync(
            rows, ImportFileType.ProductSerial, mappings, 1, false, operationId);
        var replay = await service.ExecuteImportAsync(
            rows, ImportFileType.ProductSerial, mappings, 1, false, operationId);

        Assert.Equal(1, first.SuccessCount);
        Assert.Equal(1, replay.SuccessCount);
        using var assertion = DatabaseHelper.CreateContext(connection);
        Assert.Single(assertion.ProductSerials.Where(item => item.ProductId == 1698));
        Assert.Single(assertion.StockLedgers.Where(item => item.ProductId == 1698));
        Assert.Equal(1m, assertion.StockBalances.Single(item => item.ProductId == 1698).OnHandQuantity);
    }

    [Fact]
    public void Stock_in_second_line_validation_failure_rolls_back_entire_document()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.AddRange(
                CreateProduct(1700, "IMPORT-NON-SERIAL", serialTracked: false),
                CreateProduct(1701, "IMPORT-SERIAL", serialTracked: true));
            db.SaveChanges();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var result = service.ExecuteImport(
            new List<Dictionary<string, string>>
            {
                StockInRow("SI-ROLLBACK-001", "IMPORT-NON-SERIAL", "2", string.Empty),
                StockInRow("SI-ROLLBACK-001", "IMPORT-SERIAL", "2", "ONLY-ONE-SERIAL")
            },
            ImportFileType.StockIn,
            StockInMappings(),
            userId: 1,
            autoCreateReferences: false);

        Assert.Equal(0, result.SuccessCount);
        Assert.NotEmpty(result.Errors);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.StockIns.Where(item => item.DocumentCode == "SI-ROLLBACK-001"));
        Assert.Empty(assertContext.StockInLines.Where(line => line.ProductId == 1700 || line.ProductId == 1701));
        Assert.Empty(assertContext.ProductSerials.Where(serial => serial.SerialNumber == "ONLY-ONE-SERIAL"));
        Assert.Empty(assertContext.StockLedgers.Where(ledger => ledger.ProductId == 1700 || ledger.ProductId == 1701));
        Assert.Empty(assertContext.StockBalances.Where(balance => balance.ProductId == 1700 || balance.ProductId == 1701));
    }

    [Fact]
    public void Duplicate_serial_group_rolls_back_without_undoing_previous_valid_group()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.Add(CreateProduct(1705, "IMPORT-GROUPED-SERIAL", serialTracked: true));
            db.ProductSerials.Add(new ProductSerial
            {
                ProductId = 1705,
                SerialNumber = "EXISTING-SERIAL",
                CurrentStatus = "InStock",
                CurrentWarehouseId = 1,
                LastStockInLineId = 1
            });
            db.SaveChanges();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var result = service.ExecuteImport(
            new List<Dictionary<string, string>>
            {
                StockInRow("SI-GROUP-VALID", "IMPORT-GROUPED-SERIAL", "1", "NEW-GROUP-SERIAL"),
                StockInRow("SI-GROUP-DUPLICATE", "IMPORT-GROUPED-SERIAL", "1", "EXISTING-SERIAL")
            },
            ImportFileType.StockIn,
            StockInMappings(),
            userId: 1,
            autoCreateReferences: false);

        Assert.Equal(1, result.SuccessCount);
        Assert.Single(result.Errors);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Contains(assertContext.StockIns, item => item.DocumentCode == "SI-GROUP-VALID");
        Assert.DoesNotContain(assertContext.StockIns, item => item.DocumentCode == "SI-GROUP-DUPLICATE");
        Assert.Contains(assertContext.ProductSerials, serial => serial.SerialNumber == "NEW-GROUP-SERIAL");
        Assert.Equal(1m, assertContext.StockBalances.Single(balance => balance.ProductId == 1705).OnHandQuantity);
    }

    [Fact]
    public async Task Stock_in_technical_failure_in_second_group_rolls_back_first_group()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.Add(CreateProduct(1706, "IMPORT-TECH-IN", serialTracked: false));
            db.SaveChanges();
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailSecondStockInGroup
                BEFORE INSERT ON StockIn
                WHEN NEW.DocumentCode = 'SI-TECH-SECOND'
                BEGIN
                    SELECT RAISE(ABORT, 'forced stock-in provider failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => service.ExecuteImportAsync(
            new List<Dictionary<string, string>>
            {
                StockInRow("SI-TECH-FIRST", "IMPORT-TECH-IN", "1", string.Empty),
                StockInRow("SI-TECH-SECOND", "IMPORT-TECH-IN", "1", string.Empty)
            },
            ImportFileType.StockIn,
            StockInMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Contains("forced stock-in provider failure", error.ToString(), StringComparison.Ordinal);
        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.DoesNotContain(assertContext.StockIns, item =>
            item.DocumentCode == "SI-TECH-FIRST" || item.DocumentCode == "SI-TECH-SECOND");
        Assert.DoesNotContain(assertContext.StockBalances, balance => balance.ProductId == 1706);
        Assert.DoesNotContain(assertContext.StockLedgers, ledger => ledger.ProductId == 1706);
    }

    [Fact]
    public async Task Stock_out_technical_failure_in_second_group_rolls_back_first_group()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.Add(CreateProduct(1707, "IMPORT-TECH-OUT", serialTracked: false));
            db.StockBalances.Add(new StockBalance
            {
                ProductId = 1707,
                WarehouseId = 1,
                OnHandQuantity = 10m,
                AvailableQuantity = 10m
            });
            db.SaveChanges();
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailSecondStockOutGroup
                BEFORE INSERT ON StockOut
                WHEN NEW.DocumentCode = 'SO-TECH-SECOND'
                BEGIN
                    SELECT RAISE(ABORT, 'forced stock-out provider failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => service.ExecuteImportAsync(
            new List<Dictionary<string, string>>
            {
                StockOutRow("SO-TECH-FIRST", "IMPORT-TECH-OUT", "1"),
                StockOutRow("SO-TECH-SECOND", "IMPORT-TECH-OUT", "1")
            },
            ImportFileType.StockOut,
            StockOutMappings(),
            1,
            false,
            Guid.NewGuid()));

        Assert.Contains("forced stock-out provider failure", error.ToString(), StringComparison.Ordinal);
        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.DoesNotContain(assertContext.StockOuts, item =>
            item.DocumentCode == "SO-TECH-FIRST" || item.DocumentCode == "SO-TECH-SECOND");
        var balance = assertContext.StockBalances.Single(item => item.ProductId == 1707);
        Assert.Equal(10m, balance.OnHandQuantity);
        Assert.Equal(10m, balance.AvailableQuantity);
        Assert.DoesNotContain(assertContext.StockLedgers, ledger => ledger.ProductId == 1707);
    }

    [Theory]
    [InlineData(ImportFileType.StockIn, "SI-EXPLICIT-REPLAY")]
    [InlineData(ImportFileType.StockOut, "SO-EXPLICIT-REPLAY")]
    public async Task Explicit_stock_code_replays_exact_payload_and_rejects_different_same_count_payload(
        ImportFileType type,
        string documentCode)
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.Add(CreateProduct(1708, "IMPORT-EXPLICIT-REPLAY", serialTracked: false));
            if (type == ImportFileType.StockOut)
            {
                db.StockBalances.Add(new StockBalance
                {
                    ProductId = 1708,
                    WarehouseId = 1,
                    OnHandQuantity = 10m,
                    AvailableQuantity = 10m
                });
            }
            db.SaveChanges();
        }

        var operationId = Guid.NewGuid();
        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var mappings = type == ImportFileType.StockIn ? StockInMappings() : StockOutMappings();
        var rows = type == ImportFileType.StockIn
            ? new List<Dictionary<string, string>>
            {
                StockInRow(documentCode, "IMPORT-EXPLICIT-REPLAY", "1", string.Empty)
            }
            : new List<Dictionary<string, string>>
            {
                StockOutRow(documentCode, "IMPORT-EXPLICIT-REPLAY", "1")
            };

        var first = await service.ExecuteImportAsync(
            rows, type, mappings, 1, false, operationId);
        var replay = await service.ExecuteImportAsync(
            rows, type, mappings, 1, false, operationId);
        var differentOperation = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteImportAsync(
                rows, type, mappings, 1, false, Guid.NewGuid()));
        rows[0]["Quantity"] = "2";

        var mismatch = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteImportAsync(
                rows, type, mappings, 1, false, operationId));

        Assert.Equal(1, first.SuccessCount);
        Assert.Equal(1, replay.SuccessCount);
        Assert.Empty(first.Errors);
        Assert.Empty(replay.Errors);
        Assert.Contains("payload", differentOperation.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("payload", mismatch.Message, StringComparison.OrdinalIgnoreCase);
        using var assertContext = DatabaseHelper.CreateContext(connection);
        if (type == ImportFileType.StockIn)
        {
            var document = Assert.Single(assertContext.StockIns.Where(item => item.DocumentCode == documentCode));
            Assert.Single(assertContext.StockInLines.Where(line => line.StockInId == document.Id));
        }
        else
        {
            var document = Assert.Single(assertContext.StockOuts.Where(item => item.DocumentCode == documentCode));
            Assert.Single(assertContext.StockOutLines.Where(line => line.StockOutId == document.Id));
        }
    }

    [Fact]
    public void Stock_out_second_line_failure_rolls_back_first_line_posting()
    {
        using var connection = OpenDatabase();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            db.Products.AddRange(
                CreateProduct(1710, "IMPORT-AVAILABLE", serialTracked: false),
                CreateProduct(1711, "IMPORT-EMPTY", serialTracked: false));
            db.StockBalances.AddRange(
                new StockBalance
                {
                    ProductId = 1710,
                    WarehouseId = 1,
                    OnHandQuantity = 2m,
                    AvailableQuantity = 2m
                },
                new StockBalance
                {
                    ProductId = 1711,
                    WarehouseId = 1,
                    OnHandQuantity = 0m,
                    AvailableQuantity = 0m
                });
            db.SaveChanges();
        }

        var service = new DynamicImportService(() => DatabaseHelper.CreateContext(connection));
        var result = service.ExecuteImport(
            new List<Dictionary<string, string>>
            {
                StockOutRow("SO-ROLLBACK-001", "IMPORT-AVAILABLE", "2"),
                StockOutRow("SO-ROLLBACK-001", "IMPORT-EMPTY", "1")
            },
            ImportFileType.StockOut,
            StockOutMappings(),
            userId: 1,
            autoCreateReferences: false);

        Assert.Equal(0, result.SuccessCount);
        Assert.NotEmpty(result.Errors);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Empty(assertContext.StockOuts.Where(item => item.DocumentCode == "SO-ROLLBACK-001"));
        Assert.Empty(assertContext.StockOutLines.Where(line => line.ProductId == 1710 || line.ProductId == 1711));
        Assert.Empty(assertContext.StockLedgers.Where(ledger => ledger.ProductId == 1710 || ledger.ProductId == 1711));
        var restored = assertContext.StockBalances.Single(balance =>
            balance.ProductId == 1710 && balance.WarehouseId == 1);
        Assert.Equal(2m, restored.OnHandQuantity);
        Assert.Equal(2m, restored.AvailableQuantity);
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        return connection;
    }

    private static Product CreateProduct(int id, string code, bool serialTracked)
    {
        return new Product
        {
            Id = id,
            ProductCode = code,
            DisplayName = code,
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true,
            IsSerialTracked = serialTracked
        };
    }

    private static Dictionary<string, string> StockInRow(
        string documentCode,
        string productCode,
        string quantity,
        string serialNumbers)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentCode"] = documentCode,
            ["ImportDate"] = "2026-07-13",
            ["SupplierName"] = string.Empty,
            ["WarehouseName"] = string.Empty,
            ["Notes"] = "rollback test",
            ["ProductCode"] = productCode,
            ["Quantity"] = quantity,
            ["SerialNumbers"] = serialNumbers
        };
    }

    private static Dictionary<string, string> StockOutRow(
        string documentCode,
        string productCode,
        string quantity)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentCode"] = documentCode,
            ["ExportDate"] = "2026-07-13",
            ["CustomerName"] = string.Empty,
            ["WarehouseName"] = string.Empty,
            ["Notes"] = "rollback test",
            ["ProductCode"] = productCode,
            ["Quantity"] = quantity,
            ["SerialNumbers"] = string.Empty
        };
    }

    private static Dictionary<string, string> StockInMappings()
    {
        return IdentityMappings(
            "DocumentCode",
            "ImportDate",
            "SupplierName",
            "WarehouseName",
            "Notes",
            "ProductCode",
            "Quantity",
            "SerialNumbers");
    }

    private static Dictionary<string, string> StockOutMappings()
    {
        return IdentityMappings(
            "DocumentCode",
            "ExportDate",
            "CustomerName",
            "WarehouseName",
            "Notes",
            "ProductCode",
            "Quantity",
            "SerialNumbers");
    }

    private static Dictionary<string, string> IdentityMappings(params string[] keys)
    {
        return keys.ToDictionary(key => key, key => key, StringComparer.OrdinalIgnoreCase);
    }
}
