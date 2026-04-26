using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class PostStockOutTests
{
    [Fact]
    public void PostApprovedStockOut_decrements_available_stock_and_marks_serials_sold()
    {
        var store = new InMemoryInventoryStore();
        store.Products[30] = new ProductSnapshot(30, true);
        store.Balances[(30, 1)] = new StockBalanceSnapshot(30, 1, 3, 3, 0);
        store.Serials["SALE-001"] = new ProductSerialSnapshot("SALE-001", 30, 1, SerialStatus.InStock);
        store.Serials["SALE-002"] = new ProductSerialSnapshot("SALE-002", 30, 1, SerialStatus.InStock);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 10, 0, 0)));
        var command = new PostStockOutCommand(
            DocumentId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Kind: StockOutKind.Sale,
            Status: StockDocumentStatus.Approved,
            ProductId: 30,
            Quantity: 2,
            SerialNumbers: new[] { "SALE-001", "SALE-002" },
            PostedByUserId: 8);

        service.PostStockOut(command);

        var balance = store.Balances[(30, 1)];
        Assert.Equal(1, balance.OnHandQuantity);
        Assert.Equal(1, balance.AvailableQuantity);
        Assert.Equal(SerialStatus.Sold, store.Serials["SALE-001"].Status);
        Assert.Null(store.Serials["SALE-001"].CurrentWarehouseId);
        Assert.Equal(SerialStatus.Sold, store.Serials["SALE-002"].Status);
        Assert.Null(store.Serials["SALE-002"].CurrentWarehouseId);
        Assert.Single(store.Ledgers);
        Assert.Equal(StockLedgerDirection.Out, store.Ledgers[0].Direction);
        Assert.Single(store.Audits);
        Assert.Equal(AuditActionCode.PostStockOut, store.Audits[0].ActionCode);
        Assert.Equal(StockDocumentStatus.Posted, store.DocumentStatuses[command.DocumentId]);
        Assert.True(store.WasCommitted);
    }
}
