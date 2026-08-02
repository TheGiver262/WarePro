using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class StockTransferSerialBaseQuantityTests
{
    [Fact]
    public void PostTransfer_matches_serial_count_to_base_quantity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setup = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(setup);
            setup.AppUsers.Find(1)!.RoleCode = "Quản lý";
            setup.Warehouses.Add(new Warehouse
            {
                Id = 2,
                WarehouseCode = "WH2",
                DisplayName = "Second Warehouse",
                IsActive = true
            });
            setup.Products.Add(new Product
            {
                Id = 121,
                ProductCode = "PAIR-SERIAL",
                DisplayName = "Serial pair",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 200m,
                IsActive = true,
                IsSerialTracked = true
            });
            setup.StockBalances.Add(new StockBalance
            {
                ProductId = 121,
                WarehouseId = 1,
                OnHandQuantity = 2m,
                AvailableQuantity = 2m,
                ReservedQuantity = 0m
            });
            var line = new StockTransferLine
            {
                ProductId = 121,
                UnitId = 1,
                Quantity = 1m,
                BaseQuantity = 2m
            };
            line.ProductSerials.Add(new ProductSerial
            {
                ProductId = 121,
                SerialNumber = "PAIR-001",
                CurrentWarehouseId = 1,
                CurrentStatus = "InStock",
                LastStockInLineId = 0
            });
            line.ProductSerials.Add(new ProductSerial
            {
                ProductId = 121,
                SerialNumber = "PAIR-002",
                CurrentWarehouseId = 1,
                CurrentStatus = "InStock",
                LastStockInLineId = 0
            });
            setup.StockTransfers.Add(new StockTransfer
            {
                Id = 701,
                DocumentCode = "ST-SERIAL-BASE",
                FromWarehouseId = 1,
                ToWarehouseId = 2,
                Status = "Draft",
                TransferDate = new DateTime(2026, 7, 13),
                CreatedBy = 1,
                CreatedAt = new DateTime(2026, 7, 13),
                Lines = new List<StockTransferLine> { line }
            });
            setup.SaveChanges();
        }

        var service = new StockTransferService(() => DatabaseHelper.CreateContext(connection));
        service.SubmitForApproval(701, 1);
        service.Approve(701, 1);
        service.Post(701, 1);

        using var verify = DatabaseHelper.CreateContext(connection);
        var serials = verify.ProductSerials.Where(serial => serial.ProductId == 121).ToList();
        Assert.Equal(2, serials.Count);
        Assert.All(serials, serial => Assert.Equal(2, serial.CurrentWarehouseId));
        Assert.All(serials, serial => Assert.Equal("InStock", serial.CurrentStatus));
    }
    [Fact]
    public void PostTransfer_query_count_does_not_grow_per_line()
    {
        var singleLineCount = CountPostSelects(1);
        var sixLineCount = CountPostSelects(6);

        Assert.True(
            sixLineCount <= singleLineCount + 2,
            $"Expected at most {singleLineCount + 2} SELECTs, but observed {sixLineCount}.");
    }

    private static int CountPostSelects(int lineCount)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setup = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(setup);
            setup.Warehouses.Add(new Warehouse
            {
                Id = 2,
                WarehouseCode = "WH2-N1",
                DisplayName = "Transfer destination",
                IsActive = true
            });

            var lines = new List<StockTransferLine>();
            foreach (var index in Enumerable.Range(0, lineCount))
            {
                var productId = 700 + index;
                setup.Products.Add(new Product
                {
                    Id = productId,
                    ProductCode = $"N1-TRANSFER-{index}",
                    DisplayName = $"N+1 transfer product {index}",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsActive = true,
                    IsSerialTracked = true
                });
                setup.StockBalances.Add(new StockBalance
                {
                    ProductId = productId,
                    WarehouseId = 1,
                    OnHandQuantity = 1m,
                    AvailableQuantity = 1m
                });
                var line = new StockTransferLine
                {
                    ProductId = productId,
                    UnitId = 1,
                    Quantity = 1m,
                    BaseQuantity = 1m
                };
                line.ProductSerials.Add(new ProductSerial
                {
                    ProductId = productId,
                    SerialNumber = $"N1-TRANSFER-SN-{index}",
                    CurrentWarehouseId = 1,
                    CurrentStatus = "InStock",
                    LastStockInLineId = 0
                });
                lines.Add(line);
            }

            setup.StockTransfers.Add(new StockTransfer
            {
                Id = 702,
                DocumentCode = $"ST-N1-{lineCount}",
                FromWarehouseId = 1,
                ToWarehouseId = 2,
                Status = "Draft",
                TransferDate = DateTime.UtcNow,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                Lines = lines
            });
            setup.SaveChanges();
        }

        var counter = new SelectCommandCounter();
        var service = new StockTransferService(
            () => DatabaseHelper.CreateContext(connection, counter));
        service.SubmitForApproval(702, 1);
        service.Approve(702, 1);
        counter.Reset();

        service.Post(702, 1);

        return counter.Count;
    }
}
