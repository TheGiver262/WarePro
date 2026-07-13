using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services.DataImport;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class DynamicStockImportServiceTests
{
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
