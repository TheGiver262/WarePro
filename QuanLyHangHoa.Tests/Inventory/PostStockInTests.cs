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
        var command = new PostStockInCommand(
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
}
