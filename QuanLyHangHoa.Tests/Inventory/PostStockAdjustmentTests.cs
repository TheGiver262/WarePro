using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class PostStockAdjustmentTests
{
    [Fact]
    public void PostApprovedAdjustmentIn_increases_balance_and_writes_ledger_and_audit()
    {
        var store = new InMemoryInventoryStore();
        store.Products[60] = new ProductSnapshot(60, false);
        store.Balances[(60, 1)] = new StockBalanceSnapshot(60, 1, 2, 2, 0);
        var service = new InventoryAdjustmentService(
            store,
            new FixedWarehouseProvider(1),
            new FixedClock(new DateTime(2026, 4, 27, 13, 0, 0)));
        var command = new PostStockAdjustmentCommand(
            101,
            StockDocumentStatus.Approved,
            "COUNT-001",
            "Stock count surplus",
            new[]
            {
                new StockAdjustmentLineCommand(60, StockLedgerDirection.In, 3, Array.Empty<string>())
            },
            9);

        service.PostAdjustment(command);

        var balance = store.Balances[(60, 1)];
        Assert.Equal(5, balance.OnHandQuantity);
        Assert.Equal(5, balance.AvailableQuantity);
        Assert.Single(store.Ledgers);
        Assert.Equal(StockLedgerDirection.In, store.Ledgers[0].Direction);
        Assert.Equal(3, store.Ledgers[0].Quantity);
        Assert.Single(store.Audits);
        Assert.Equal(AuditActionCode.PostStockAdjustment, store.Audits[0].ActionCode);
        Assert.Equal(StockDocumentStatus.Posted, store.DocumentStatuses[(command.DocumentId, "StockAdjustment")]);
        Assert.True(store.WasCommitted);
    }

    [Fact]
    public void PostApprovedAdjustmentIn_for_serial_product_creates_in_stock_serials()
    {
        var store = new InMemoryInventoryStore();
        store.Products[61] = new ProductSnapshot(61, true);
        var service = new InventoryAdjustmentService(
            store,
            new FixedWarehouseProvider(1),
            new FixedClock(new DateTime(2026, 4, 27, 13, 15, 0)));
        var command = new PostStockAdjustmentCommand(
            102,
            StockDocumentStatus.Approved,
            "COUNT-002",
            "Found serials during stock count",
            new[]
            {
                new StockAdjustmentLineCommand(61, StockLedgerDirection.In, 2, new[] { "ADJ-IN-001", "ADJ-IN-002" })
            },
            9);

        service.PostAdjustment(command);

        Assert.Equal(2, store.Serials.Count);
        Assert.Equal(SerialStatus.InStock, store.Serials["ADJ-IN-001"].Status);
        Assert.Equal(1, store.Serials["ADJ-IN-001"].CurrentWarehouseId);
        Assert.Equal(SerialStatus.InStock, store.Serials["ADJ-IN-002"].Status);
        Assert.Equal(1, store.Serials["ADJ-IN-002"].CurrentWarehouseId);
    }

    [Fact]
    public void PostApprovedAdjustmentOut_for_serial_product_marks_serials_inactive()
    {
        var store = new InMemoryInventoryStore();
        store.Products[62] = new ProductSnapshot(62, true);
        store.Balances[(62, 1)] = new StockBalanceSnapshot(62, 1, 2, 2, 0);
        store.Serials["ADJ-OUT-001"] = new ProductSerialSnapshot("ADJ-OUT-001", 62, 1, SerialStatus.InStock);
        var service = new InventoryAdjustmentService(
            store,
            new FixedWarehouseProvider(1),
            new FixedClock(new DateTime(2026, 4, 27, 13, 30, 0)));
        var command = new PostStockAdjustmentCommand(
            103,
            StockDocumentStatus.Approved,
            "COUNT-003",
            "Missing serial during stock count",
            new[]
            {
                new StockAdjustmentLineCommand(62, StockLedgerDirection.Out, 1, new[] { "ADJ-OUT-001" })
            },
            9);

        service.PostAdjustment(command);

        var balance = store.Balances[(62, 1)];
        Assert.Equal(1, balance.OnHandQuantity);
        Assert.Equal(1, balance.AvailableQuantity);
        Assert.Equal(SerialStatus.Inactive, store.Serials["ADJ-OUT-001"].Status);
        Assert.Null(store.Serials["ADJ-OUT-001"].CurrentWarehouseId);
    }
}
