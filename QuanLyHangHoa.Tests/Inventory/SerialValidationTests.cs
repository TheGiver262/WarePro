using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class SerialValidationTests
{
    [Fact]
    public void Duplicate_serials_in_stock_in_command_are_rejected()
    {
        var store = new InMemoryInventoryStore();
        store.Products[50] = new ProductSnapshot(50, true);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 11, 0, 0)));
        var command = new PostStockInCommand(
            401, WarehouseId: 1,
            StockInKind.OpeningBalance,
            StockDocumentStatus.Approved,
            50,
            2,
            new[] { "DUP-001", "dup-001" },
            7);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(command));

        Assert.Equal("Duplicate serials are not allowed.", ex.Message);
        Assert.Empty(store.Serials);
        Assert.False(store.WasCommitted);
    }

    [Fact]
    public void Stock_in_rejects_serial_that_already_exists()
    {
        var store = new InMemoryInventoryStore();
        store.Products[50] = new ProductSnapshot(50, true);
        store.Serials["EXISTING-001"] = new ProductSerialSnapshot("EXISTING-001", 50, 1, SerialStatus.InStock);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 11, 10, 0)));
        var command = new PostStockInCommand(
            402, WarehouseId: 1,
            StockInKind.OpeningBalance,
            StockDocumentStatus.Approved,
            50,
            1,
            new[] { "EXISTING-001" },
            7);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(command));

        Assert.Equal("Serial EXISTING-001 already exists.", ex.Message);
        Assert.Single(store.Serials);
        Assert.False(store.WasCommitted);
    }

    [Fact]
    public void Stock_out_rejects_serial_that_is_not_in_stock()
    {
        var store = new InMemoryInventoryStore();
        store.Products[51] = new ProductSnapshot(51, true);
        store.Balances[(51, 1)] = new StockBalanceSnapshot(51, 1, 1, 1, 0);
        store.Serials["SOLD-001"] = new ProductSerialSnapshot("SOLD-001", 51, null, SerialStatus.Sold);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 11, 15, 0)));
        var command = new PostStockOutCommand(
            403, WarehouseId: 1,
            StockOutKind.Sale,
            StockDocumentStatus.Approved,
            51,
            1,
            new[] { "SOLD-001" },
            8);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockOut(command));

        Assert.Equal("Serial SOLD-001 is not in the specified warehouse.", ex.Message);
        Assert.Empty(store.Ledgers);
        Assert.Empty(store.Audits);
        Assert.False(store.WasCommitted);
    }
}
