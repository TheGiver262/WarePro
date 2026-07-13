using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class StockTransferAndConcurrencyTests
{
    [Fact]
    public void PostTransfer_uses_base_quantity_for_balances_and_ledgers()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setup = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(setup);
            setup.Warehouses.Add(new Warehouse
            {
                Id = 2,
                WarehouseCode = "WH2",
                DisplayName = "Second Warehouse",
                IsActive = true
            });
            setup.Products.Add(new Product
            {
                Id = 120,
                ProductCode = "BOX-12",
                DisplayName = "Box of 12",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 120m,
                IsActive = true,
                IsSerialTracked = false
            });
            setup.StockBalances.Add(new StockBalance
            {
                ProductId = 120,
                WarehouseId = 1,
                OnHandQuantity = 24m,
                AvailableQuantity = 24m,
                ReservedQuantity = 0m
            });
            setup.StockTransfers.Add(new StockTransfer
            {
                Id = 700,
                DocumentCode = "ST-BASE-24",
                FromWarehouseId = 1,
                ToWarehouseId = 2,
                Status = "Draft",
                TransferDate = new DateTime(2026, 7, 13),
                CreatedBy = 1,
                CreatedAt = new DateTime(2026, 7, 13),
                Lines = new List<StockTransferLine>
                {
                    new()
                    {
                        ProductId = 120,
                        UnitId = 1,
                        Quantity = 2m,
                        BaseQuantity = 24m
                    }
                }
            });
            setup.SaveChanges();
        }

        var service = new StockTransferService(() => DatabaseHelper.CreateContext(connection));
        service.Post(700, 1);

        using var verify = DatabaseHelper.CreateContext(connection);
        var source = verify.StockBalances.Single(balance => balance.ProductId == 120 && balance.WarehouseId == 1);
        var destination = verify.StockBalances.Single(balance => balance.ProductId == 120 && balance.WarehouseId == 2);
        Assert.Equal(0m, source.OnHandQuantity);
        Assert.Equal(0m, source.AvailableQuantity);
        Assert.Equal(24m, destination.OnHandQuantity);
        Assert.Equal(24m, destination.AvailableQuantity);
        var ledgers = verify.StockLedgers.Where(ledger => ledger.SourceDocumentId == 700).ToList();
        Assert.Equal(2, ledgers.Count);
        Assert.All(ledgers, ledger => Assert.Equal(24m, ledger.Quantity));
    }

    [Fact]
    public void Commit_rejects_second_writer_based_on_stale_balance()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setup = DatabaseHelper.CreateContext(connection))
        {
            setup.Database.EnsureCreated();
            setup.StockBalances.Add(new StockBalance
            {
                Id = 900,
                ProductId = 1,
                WarehouseId = 1,
                OnHandQuantity = 10m,
                AvailableQuantity = 10m,
                ReservedQuantity = 0m
            });
            setup.SaveChanges();
        }

        using var firstContext = DatabaseHelper.CreateContext(connection);
        using var secondContext = DatabaseHelper.CreateContext(connection);
        var first = new EfInventoryUnitOfWork(firstContext);
        var second = new EfInventoryUnitOfWork(secondContext);
        var firstSnapshot = first.FindBalance(1, 1)!;
        var secondSnapshot = second.FindBalance(1, 1)!;
        first.SaveBalance(firstSnapshot with { OnHandQuantity = 3m, AvailableQuantity = 3m });
        second.SaveBalance(secondSnapshot with { OnHandQuantity = 3m, AvailableQuantity = 3m });

        first.Commit();
        var exception = Assert.Throws<InventoryDomainException>(() => second.Commit());

        Assert.Equal("Tồn kho vừa thay đổi. Vui lòng tải lại và thử lại.", exception.Message);
        using var verify = DatabaseHelper.CreateContext(connection);
        var persisted = verify.StockBalances.Single(balance => balance.Id == 900);
        Assert.Equal(3m, persisted.OnHandQuantity);
        Assert.Equal(3m, persisted.AvailableQuantity);
    }
}
