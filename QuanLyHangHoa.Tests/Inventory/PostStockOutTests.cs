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
        var command = new PostStockOutCommand(WarehouseId: 1, 
            DocumentId: 301,
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
        Assert.Equal(2, store.Ledgers[0].Quantity);
        Assert.Equal(30, store.Ledgers[0].ProductId);
        Assert.Equal(1, store.Ledgers[0].WarehouseId);
        Assert.Equal(8, store.Ledgers[0].PostedByUserId);
        Assert.Equal(new DateTime(2026, 4, 26, 10, 0, 0), store.Ledgers[0].PostedAt);
        Assert.Single(store.Audits);
        Assert.Equal(AuditActionCode.PostStockOut, store.Audits[0].ActionCode);
        Assert.Equal(StockDocumentStatus.Posted, store.DocumentStatuses[(command.DocumentId, "StockOut")]);
        Assert.True(store.WasCommitted);
    }

    [Fact]
    public void PostStockOut_rejects_warranty_replacement_until_replacement_flow_is_implemented()
    {
        var store = new InMemoryInventoryStore();
        store.Products[30] = new ProductSnapshot(30, true);
        store.Balances[(30, 1)] = new StockBalanceSnapshot(30, 1, 3, 3, 0);
        store.Serials["WR-001"] = new ProductSerialSnapshot("WR-001", 30, 1, SerialStatus.InStock);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 10, 0, 0)));
        var command = new PostStockOutCommand(WarehouseId: 1, 
            DocumentId: 302,
            Kind: StockOutKind.WarrantyReplacement,
            Status: StockDocumentStatus.Approved,
            ProductId: 30,
            Quantity: 1,
            SerialNumbers: new[] { "WR-001" },
            PostedByUserId: 8);

        var exception = Assert.Throws<InventoryDomainException>(() => service.PostStockOut(command));

        Assert.Equal("Only sale stock-out can be posted by this service.", exception.Message);
        var balance = store.Balances[(30, 1)];
        Assert.Equal(3, balance.OnHandQuantity);
        Assert.Equal(3, balance.AvailableQuantity);
        Assert.Equal(SerialStatus.InStock, store.Serials["WR-001"].Status);
        Assert.Equal(1, store.Serials["WR-001"].CurrentWarehouseId);
        Assert.Empty(store.Ledgers);
        Assert.Empty(store.Audits);
        Assert.False(store.DocumentStatuses.ContainsKey((command.DocumentId, "StockOut")));
        Assert.False(store.WasCommitted);
    }

    [Fact]
    public void PostStockOut_with_insufficient_available_stock_does_not_commit_any_changes()
    {
        var store = new InMemoryInventoryStore();
        store.Products[40] = new ProductSnapshot(40, false);
        store.Balances[(40, 1)] = new StockBalanceSnapshot(40, 1, 1, 1, 0);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 10, 30, 0)));
        var command = new PostStockOutCommand(WarehouseId: 1, 
            DocumentId: 303,
            Kind: StockOutKind.Sale,
            Status: StockDocumentStatus.Approved,
            ProductId: 40,
            Quantity: 2,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 8);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockOut(command));

        Assert.Equal("Insufficient available stock.", ex.Message);
        var balance = store.Balances[(40, 1)];
        Assert.Equal(1, balance.OnHandQuantity);
        Assert.Equal(1, balance.AvailableQuantity);
        Assert.Empty(store.Ledgers);
        Assert.Empty(store.Audits);
        Assert.False(store.DocumentStatuses.ContainsKey((command.DocumentId, "StockOut")));
        Assert.False(store.WasCommitted);
    }

    [Fact]
    public void PostStockOut_with_no_balance_does_not_create_balance_or_commit_changes()
    {
        var store = new InMemoryInventoryStore();
        store.Products[41] = new ProductSnapshot(41, false);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 10, 45, 0)));
        var command = new PostStockOutCommand(WarehouseId: 1, 
            DocumentId: 304,
            Kind: StockOutKind.Sale,
            Status: StockDocumentStatus.Approved,
            ProductId: 41,
            Quantity: 1,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 8);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockOut(command));

        Assert.Equal("Insufficient available stock.", ex.Message);
        Assert.Empty(store.Balances);
        Assert.Empty(store.Ledgers);
        Assert.Empty(store.Audits);
        Assert.False(store.DocumentStatuses.ContainsKey((command.DocumentId, "StockOut")));
        Assert.False(store.WasCommitted);
    }
}
