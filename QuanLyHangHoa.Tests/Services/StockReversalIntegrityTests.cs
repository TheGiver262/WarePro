using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using QuanLyHangHoa.ViewModels;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class StockReversalIntegrityTests
{
    [Fact]
    public void Reverse_stock_in_restores_balance_serials_source_and_audit_once()
    {
        using var connection = OpenDatabase();
        int sourceId;

        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var product = AddProduct(db, 701, "REV-IN", serialTracked: true);
            var source = new StockIn
            {
                DocumentCode = "PIN-REV-001",
                WarehouseId = 1,
                PurposeCode = "Purchase",
                Status = "Posted",
                CreatedBy = 1,
                PostedBy = 1,
                CreatedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow
            };
            var line = new StockInLine
            {
                StockIn = source,
                Product = product,
                UnitId = 1,
                Quantity = 2m,
                BaseQuantity = 2m,
                UnitPrice = 10m
            };
            db.StockInLines.Add(line);
            db.StockBalances.Add(new StockBalance
            {
                WarehouseId = 1,
                ProductId = product.Id,
                OnHandQuantity = 2m,
                AvailableQuantity = 2m
            });
            db.SaveChanges();

            db.ProductSerials.AddRange(
                CreateSerial(product.Id, line.Id, "REV-IN-001", "InStock", 1),
                CreateSerial(product.Id, line.Id, "REV-IN-002", "InStock", 1));
            db.StockLedgers.Add(new StockLedger
            {
                SourceDocumentType = "StockIn",
                SourceDocumentId = source.Id,
                WarehouseId = 1,
                ProductId = product.Id,
                MovementType = "In",
                Quantity = 2m,
                PostedBy = 1,
                PostedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            sourceId = source.Id;
        }

        var service = new StockReversalService(() => DatabaseHelper.CreateContext(connection));
        var reversalId = service.ReverseDocument("StockIn", sourceId, 1);

        Assert.True(reversalId > 0);
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var balance = db.StockBalances.Single(b => b.ProductId == 701 && b.WarehouseId == 1);
            Assert.Equal(0m, balance.OnHandQuantity);
            Assert.Equal(0m, balance.AvailableQuantity);

            var serials = db.ProductSerials.Where(s => s.ProductId == 701).ToList();
            Assert.All(serials, serial =>
            {
                Assert.Equal("Inactive", serial.CurrentStatus);
                Assert.Null(serial.CurrentWarehouseId);
            });

            Assert.Equal("Reversed", db.StockIns.Single(s => s.Id == sourceId).Status);
            var reversal = db.StockAdjustments.Single(a => a.Id == reversalId);
            Assert.Equal("Reversal", reversal.AdjustmentType);
            Assert.Equal("StockIn", reversal.ReferenceDocumentType);
            Assert.Equal(sourceId, reversal.ReferenceDocumentId);
            Assert.Equal(1, reversal.WarehouseId);

            var compensating = db.StockLedgers.Single(l =>
                l.SourceDocumentType == "StockAdjustment" && l.SourceDocumentId == reversalId);
            Assert.Equal("Out", compensating.MovementType);
            Assert.Equal(2m, compensating.Quantity);
            Assert.Contains(db.AuditLogs, a =>
                a.EntityName == "StockIn" && a.EntityId == sourceId && a.ActionCode == "Reverse");
        }

        var repeated = Assert.Throws<InventoryDomainException>(
            () => service.ReverseDocument("StockIn", sourceId, 1));
        Assert.Contains("đã được đảo", repeated.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reverse_stock_out_restores_balance_serials_source_and_audit_once()
    {
        using var connection = OpenDatabase();
        int sourceId;

        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var product = AddProduct(db, 702, "REV-OUT", serialTracked: true);
            var originalStockIn = new StockIn
            {
                DocumentCode = "PIN-REV-BASE",
                WarehouseId = 1,
                PurposeCode = "Purchase",
                Status = "Posted",
                CreatedBy = 1,
                PostedBy = 1,
                CreatedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow
            };
            var originalLine = new StockInLine
            {
                StockIn = originalStockIn,
                Product = product,
                UnitId = 1,
                Quantity = 2m,
                BaseQuantity = 2m,
                UnitPrice = 10m
            };
            var source = new StockOut
            {
                DocumentCode = "POUT-REV-001",
                CustomerId = 1,
                WarehouseId = 1,
                PurposeCode = "Sale",
                Status = "Posted",
                CreatedBy = 1,
                PostedBy = 1,
                CreatedAt = DateTime.UtcNow,
                PostedAt = DateTime.UtcNow
            };
            var sourceLine = new StockOutLine
            {
                StockOut = source,
                Product = product,
                UnitId = 1,
                Quantity = 2m,
                BaseQuantity = 2m,
                UnitPrice = 20m
            };
            db.StockInLines.Add(originalLine);
            db.StockOutLines.Add(sourceLine);
            db.StockBalances.Add(new StockBalance
            {
                WarehouseId = 1,
                ProductId = product.Id,
                OnHandQuantity = 0m,
                AvailableQuantity = 0m
            });
            db.SaveChanges();

            var first = CreateSerial(product.Id, originalLine.Id, "REV-OUT-001", "Sold", null);
            first.LastStockOutLineId = sourceLine.Id;
            var second = CreateSerial(product.Id, originalLine.Id, "REV-OUT-002", "Sold", null);
            second.LastStockOutLineId = sourceLine.Id;
            db.ProductSerials.AddRange(first, second);
            db.StockLedgers.Add(new StockLedger
            {
                SourceDocumentType = "StockOut",
                SourceDocumentId = source.Id,
                WarehouseId = 1,
                ProductId = product.Id,
                MovementType = "Out",
                Quantity = 2m,
                PostedBy = 1,
                PostedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            sourceId = source.Id;
        }

        var service = new StockReversalService(() => DatabaseHelper.CreateContext(connection));
        var reversalId = service.ReverseDocument("StockOut", sourceId, 1);

        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var balance = db.StockBalances.Single(b => b.ProductId == 702 && b.WarehouseId == 1);
            Assert.Equal(2m, balance.OnHandQuantity);
            Assert.Equal(2m, balance.AvailableQuantity);

            var serials = db.ProductSerials.Where(s => s.ProductId == 702).ToList();
            Assert.All(serials, serial =>
            {
                Assert.Equal("InStock", serial.CurrentStatus);
                Assert.Equal(1, serial.CurrentWarehouseId);
            });

            Assert.Equal("Reversed", db.StockOuts.Single(s => s.Id == sourceId).Status);
            var compensating = db.StockLedgers.Single(l =>
                l.SourceDocumentType == "StockAdjustment" && l.SourceDocumentId == reversalId);
            Assert.Equal("In", compensating.MovementType);
            Assert.Equal(2m, compensating.Quantity);
            Assert.Contains(db.AuditLogs, a =>
                a.EntityName == "StockOut" && a.EntityId == sourceId && a.ActionCode == "Reverse");
        }

        var repeated = Assert.Throws<InventoryDomainException>(
            () => service.ReverseDocument("StockOut", sourceId, 1));
        Assert.Contains("đã được đảo", repeated.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reversal_source_key_is_unique_in_model()
    {
        using var connection = OpenDatabase();
        using var db = DatabaseHelper.CreateContext(connection);

        var entity = db.Model.FindEntityType(typeof(StockAdjustment));
        var index = Assert.Single(entity!.GetIndexes(), candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(new[]
            {
                nameof(StockAdjustment.ReferenceDocumentType),
                nameof(StockAdjustment.ReferenceDocumentId),
                nameof(StockAdjustment.AdjustmentType)
            }));

        Assert.True(index.IsUnique);
        Assert.Contains("Reversal", index.GetFilter(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task View_model_never_reports_zero_adjustment_as_success()
    {
        string? shownMessage = null;
        string? shownTitle = null;
        var viewModel = new StockReversalViewModel(
            new AppUser { Id = 7, Username = "admin" },
            (_, _, _, _, _, _) => Task.FromResult(0),
            (message, title) =>
            {
                shownMessage = message;
                shownTitle = title;
            })
        {
            DocumentType = "StockIn",
            DocumentIdText = "999",
            Reason = "Nhập nhầm"
        };

        await viewModel.ReverseDocumentCommand.ExecuteAsync(null);

        Assert.Equal("Không tìm thấy chứng từ kho đã ghi sổ.", viewModel.StatusMessage);
        Assert.Equal(viewModel.StatusMessage, shownMessage);
        Assert.Equal("Lỗi đảo chứng từ", shownTitle);
        Assert.DoesNotContain("Đã đảo", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        return connection;
    }

    private static Product AddProduct(AppDbContext db, int id, string code, bool serialTracked)
    {
        var product = new Product
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
        db.Products.Add(product);
        return product;
    }

    private static ProductSerial CreateSerial(
        int productId,
        int stockInLineId,
        string serialNumber,
        string status,
        int? warehouseId)
    {
        return new ProductSerial
        {
            ProductId = productId,
            LastStockInLineId = stockInLineId,
            SerialNumber = serialNumber,
            CurrentStatus = status,
            CurrentWarehouseId = warehouseId
        };
    }
}
