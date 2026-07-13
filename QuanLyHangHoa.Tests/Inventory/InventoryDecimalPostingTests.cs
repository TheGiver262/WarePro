using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class InventoryDecimalPostingTests
{
    [Fact]
    public void PostApprovedNonSerialFractionalStockIn_preserves_decimal_and_source_type()
    {
        var store = new InMemoryInventoryStore();
        store.Products[9] = new ProductSnapshot(9, false);
        var service = new InventoryPostingService(
            store,
            new FixedWarehouseProvider(1),
            new FixedClock(new DateTime(2026, 7, 13, 8, 0, 0)));

        service.PostStockIn(new PostStockInCommand(
            DocumentId: 200,
            WarehouseId: 1,
            Kind: StockInKind.Purchase,
            Status: StockDocumentStatus.Approved,
            ProductId: 9,
            Quantity: 0.5m,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 7));

        var balance = store.Balances[(9, 1)];
        Assert.Equal(0.5m, balance.OnHandQuantity);
        Assert.Equal(0.5m, balance.AvailableQuantity);
        var ledger = Assert.Single(store.Ledgers);
        Assert.Equal(0.5m, ledger.Quantity);
        Assert.Equal("StockIn", ledger.SourceDocumentType);
    }

    [Fact]
    public void PostApprovedNonSerialFractionalStockOut_preserves_decimal_and_source_type()
    {
        var store = new InMemoryInventoryStore();
        store.Products[29] = new ProductSnapshot(29, false);
        store.Balances[(29, 1)] = new StockBalanceSnapshot(29, 1, 1.5m, 1.5m, 0m);
        var service = new InventoryPostingService(
            store,
            new FixedWarehouseProvider(1),
            new FixedClock(new DateTime(2026, 7, 13, 8, 30, 0)));

        service.PostStockOut(new PostStockOutCommand(
            DocumentId: 300,
            WarehouseId: 1,
            Kind: StockOutKind.Sale,
            Status: StockDocumentStatus.Approved,
            ProductId: 29,
            Quantity: 0.5m,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 8));

        var balance = store.Balances[(29, 1)];
        Assert.Equal(1m, balance.OnHandQuantity);
        Assert.Equal(1m, balance.AvailableQuantity);
        var ledger = Assert.Single(store.Ledgers);
        Assert.Equal(0.5m, ledger.Quantity);
        Assert.Equal("StockOut", ledger.SourceDocumentType);
    }

    [Fact]
    public void EfPosting_persists_specific_stock_in_source_type()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = DatabaseHelper.CreateContext(connection);
        context.Database.EnsureCreated();
        context.Products.Add(new Product
        {
            Id = 109,
            ProductCode = "P109",
            DisplayName = "Fractional product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsSerialTracked = false
        });
        context.SaveChanges();

        var service = new InventoryPostingService(
            new EfInventoryUnitOfWork(context),
            new FixedWarehouseProvider(1),
            new FixedClock(new DateTime(2026, 7, 13, 9, 0, 0)));
        service.PostStockIn(new PostStockInCommand(
            DocumentId: 509,
            WarehouseId: 1,
            Kind: StockInKind.Purchase,
            Status: StockDocumentStatus.Approved,
            ProductId: 109,
            Quantity: 0.5m,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 1));

        var ledger = Assert.Single(context.StockLedgers.AsNoTracking());
        Assert.Equal("StockIn", ledger.SourceDocumentType);
        Assert.Equal(0.5m, ledger.Quantity);
    }
}
