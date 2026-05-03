using QuanLyHangHoa.Inventory;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class PostStockInTests
{
    [Fact]
    public void PostApprovedOpeningBalance_creates_balance_ledger_and_audit_without_purchase_invoice()
    {
        var store = new InMemoryInventoryStore();
        store.Products[10] = new ProductSnapshot(10, false);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 8, 30, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Approved,
            ProductId: 10,
            Quantity: 5,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 7);

        service.PostStockIn(command);

        var balance = store.Balances[(10, 1)];
        Assert.Equal(5, balance.OnHandQuantity);
        Assert.Equal(5, balance.AvailableQuantity);
        Assert.Equal(0, balance.ReservedQuantity);
        Assert.Single(store.Ledgers);
        Assert.Equal(StockLedgerDirection.In, store.Ledgers[0].Direction);
        Assert.Equal(5, store.Ledgers[0].Quantity);
        Assert.Single(store.Audits);
        Assert.Equal(AuditActionCode.PostStockIn, store.Audits[0].ActionCode);
        Assert.Equal(0, store.PurchaseInvoiceCreatedCount);
        Assert.Equal(StockDocumentStatus.Posted, store.DocumentStatuses[command.DocumentId]);
        Assert.True(store.WasCommitted);
    }

    [Fact]
    public void PostApprovedPurchase_creates_balance_ledger_and_audit()
    {
        var store = new InMemoryInventoryStore();
        store.Products[10] = new ProductSnapshot(10, false);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 8, 30, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Kind: StockInKind.Purchase,
            Status: StockDocumentStatus.Approved,
            ProductId: 10,
            Quantity: 1,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 7);

        service.PostStockIn(command);

        var balance = store.Balances[(10, 1)];
        Assert.Equal(1, balance.OnHandQuantity);
        Assert.Equal(1, balance.AvailableQuantity);
        Assert.Single(store.Ledgers);
        Assert.Equal(StockLedgerDirection.In, store.Ledgers[0].Direction);
        Assert.Single(store.Audits);
        Assert.Equal(AuditActionCode.PostStockIn, store.Audits[0].ActionCode);
        Assert.Equal(StockDocumentStatus.Posted, store.DocumentStatuses[command.DocumentId]);
        Assert.True(store.WasCommitted);
    }

    [Fact]
    public void PostStockIn_rejects_non_approved_status()
    {
        var store = new InMemoryInventoryStore();
        store.Products[10] = new ProductSnapshot(10, false);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 8, 30, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Draft,
            ProductId: 10,
            Quantity: 1,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 7);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(command));

        Assert.Equal("Only approved or ready-to-post stock-in documents can be posted.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PostStockIn_rejects_non_positive_quantity(int quantity)
    {
        var store = new InMemoryInventoryStore();
        store.Products[10] = new ProductSnapshot(10, false);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 8, 30, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Approved,
            ProductId: 10,
            Quantity: quantity,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 7);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(command));

        Assert.Equal("Stock-in quantity must be greater than zero.", ex.Message);
    }

    [Fact]
    public void PostStockIn_rejects_serial_numbers_for_non_serial_product()
    {
        var store = new InMemoryInventoryStore();
        store.Products[10] = new ProductSnapshot(10, false);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 8, 30, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("55555555-5555-5555-5555-555555555555"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Approved,
            ProductId: 10,
            Quantity: 1,
            SerialNumbers: new[] { "SN-001" },
            PostedByUserId: 7);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(command));

        Assert.Equal("Non-serial products cannot receive serial numbers.", ex.Message);
    }

    [Fact]
    public void PostStockIn_for_serial_product_requires_serial_count_to_match_quantity()
    {
        var store = new InMemoryInventoryStore();
        store.Products[20] = new ProductSnapshot(20, true);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 9, 0, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Approved,
            ProductId: 20,
            Quantity: 2,
            SerialNumbers: new[] { "SN-001" },
            PostedByUserId: 7);

        var ex = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(command));

        Assert.Equal("Serial count must match stock-in quantity.", ex.Message);
        Assert.Empty(store.Serials);
        Assert.Empty(store.Ledgers);
        Assert.Empty(store.Audits);
        Assert.False(store.WasCommitted);
    }

    [Fact]
    public void PostStockIn_for_serial_product_creates_in_stock_serials()
    {
        var store = new InMemoryInventoryStore();
        store.Products[21] = new ProductSnapshot(21, true);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 9, 15, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Approved,
            ProductId: 21,
            Quantity: 2,
            SerialNumbers: new[] { "SN-101", "SN-102" },
            PostedByUserId: 7);

        service.PostStockIn(command);

        Assert.Equal(2, store.Serials.Count);
        Assert.Equal(SerialStatus.InStock, store.Serials["SN-101"].Status);
        Assert.Equal(1, store.Serials["SN-101"].CurrentWarehouseId);
        Assert.Equal(SerialStatus.InStock, store.Serials["SN-102"].Status);
        Assert.Equal(1, store.Serials["SN-102"].CurrentWarehouseId);
    }

    [Fact]
    public void PostApprovedOpeningBalance_preserves_reserved_quantity_on_existing_balance()
    {
        var store = new InMemoryInventoryStore();
        store.Products[10] = new ProductSnapshot(10, false);
        store.Balances[(10, 1)] = new StockBalanceSnapshot(10, 1, 3, 1, 2);
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(new DateTime(2026, 4, 26, 8, 30, 0)));
        var command = new PostStockInCommand(WarehouseId: 1, 
            DocumentId: Guid.Parse("66666666-6666-6666-6666-666666666666"),
            Kind: StockInKind.OpeningBalance,
            Status: StockDocumentStatus.Approved,
            ProductId: 10,
            Quantity: 5,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 7);

        service.PostStockIn(command);

        var balance = store.Balances[(10, 1)];
        Assert.Equal(8, balance.OnHandQuantity);
        Assert.Equal(6, balance.AvailableQuantity);
        Assert.Equal(2, balance.ReservedQuantity);
    }
}
