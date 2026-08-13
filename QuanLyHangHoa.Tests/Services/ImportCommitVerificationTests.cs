using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class ImportCommitVerificationTests
{
    [Fact]
    public async Task Product_serial_verifier_accepts_exact_serial_identity()
    {
        using var connection = CreateDatabase();
        const string documentCode = "IMPORT-SR-exact";
        const string payloadMarker = "[import-payload-sha256:exact]";
        SeedStockIn(connection, documentCode, payloadMarker, 1501, "SER-EXPECTED");
        using (var seed = DatabaseHelper.CreateContext(connection))
        {
            seed.ProductSerials.Add(new ProductSerial
            {
                ProductId = 1501,
                SerialNumber = "SER-ORPHAN",
                CurrentStatus = "InStock",
                CurrentWarehouseId = 1,
                LastStockInLineId = null
            });
            seed.SaveChanges();
        }

        using var db = DatabaseHelper.CreateContext(connection);
        var verified = await ProductSerialImportService.VerifyCommittedBatchAsync(
            db,
            documentCode,
            payloadMarker,
            [(1501, "SER-EXPECTED")],
            CancellationToken.None);

        Assert.True(verified);
    }

    [Fact]
    public async Task Product_serial_verifier_rejects_header_with_wrong_serial_identity()
    {
        using var connection = CreateDatabase();
        const string documentCode = "IMPORT-SR-verification";
        const string payloadMarker = "[import-payload-sha256:expected]";
        SeedStockIn(connection, documentCode, payloadMarker, 1501, "SER-WRONG");

        using var db = DatabaseHelper.CreateContext(connection);
        var verified = await ProductSerialImportService.VerifyCommittedBatchAsync(
            db,
            documentCode,
            payloadMarker,
            [(1501, "SER-EXPECTED")],
            CancellationToken.None);

        Assert.False(verified);
    }

    [Fact]
    public async Task Opening_balance_verifier_accepts_exact_row()
    {
        using var connection = CreateDatabase();
        const string documentCode = "SI-OB-exact";
        const string payloadMarker = "[import-payload-sha256:exact]";
        SeedStockIn(connection, documentCode, payloadMarker, 1500, null);

        using var db = DatabaseHelper.CreateContext(connection);
        var verified = await OpeningBalanceImportService.VerifyCommittedRowsAsync(
            db,
            documentCode,
            payloadMarker,
            [(1500, 3m, string.Empty)],
            CancellationToken.None);

        Assert.True(verified);
    }

    [Fact]
    public async Task Opening_balance_verifier_rejects_header_with_missing_row()
    {
        using var connection = CreateDatabase();
        const string documentCode = "SI-OB-verification";
        const string payloadMarker = "[import-payload-sha256:expected]";
        SeedStockIn(connection, documentCode, payloadMarker, 1500, null);

        using var db = DatabaseHelper.CreateContext(connection);
        var verified = await OpeningBalanceImportService.VerifyCommittedRowsAsync(
            db,
            documentCode,
            payloadMarker,
            [(1500, 3m, string.Empty), (1501, 2m, "SER-OPEN-001,SER-OPEN-002")],
            CancellationToken.None);

        Assert.False(verified);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
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
                IsActive = true
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

    private static void SeedStockIn(
        SqliteConnection connection,
        string documentCode,
        string payloadMarker,
        int productId,
        string? serialNumber)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        var line = new StockInLine
        {
            ProductId = productId,
            UnitId = 1,
            Quantity = 3,
            BaseQuantity = 3,
            UnitPrice = 10m
        };
        if (serialNumber is not null)
        {
            line.ProductSerials.Add(new ProductSerial
            {
                ProductId = productId,
                SerialNumber = serialNumber,
                CurrentStatus = "InStock",
                CurrentWarehouseId = 1
            });
        }

        db.StockIns.Add(new StockIn
        {
            DocumentCode = documentCode,
            Status = "Posted",
            PurposeCode = "OpeningBalance",
            Notes = payloadMarker,
            WarehouseId = 1,
            ImportDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            PostedAt = DateTime.UtcNow,
            CreatedBy = 1,
            PostedBy = 1,
            Lines = [line]
        });
        db.SaveChanges();
    }
}
