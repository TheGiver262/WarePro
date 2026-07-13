using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.Tests.Inventory;

public sealed class InventoryPostingAuthorizationTests
{
    [Fact]
    public void Posting_rejects_actor_without_stock_approval_permission()
    {
        var store = CreateStore();
        store.IsStockApprovalAuthorized = false;
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(DateTime.UtcNow));

        var error = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(CreateCommand(
            StockDocumentStatus.Approved)));

        Assert.Equal("You are not authorized to approve stock documents.", error.Message);
        Assert.Empty(store.Ledgers);
    }

    [Fact]
    public void Posting_rejects_already_posted_document_to_prevent_replay()
    {
        var store = CreateStore();
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(DateTime.UtcNow));

        var error = Assert.Throws<InventoryDomainException>(() => service.PostStockIn(CreateCommand(
            StockDocumentStatus.Posted)));

        Assert.Equal("Only approved stock-in documents can be posted.", error.Message);
        Assert.Empty(store.Ledgers);
    }

    [Fact]
    public void Warranty_receive_accepts_actor_with_warranty_workflow_permission()
    {
        var store = CreateStore();
        store.IsStockApprovalAuthorized = false;
        store.IsWarrantyStockAuthorized = true;
        var service = new InventoryPostingService(store, new FixedWarehouseProvider(1), new FixedClock(DateTime.UtcNow));

        service.PostStockIn(new PostStockInCommand(
            DocumentId: 2,
            WarehouseId: 1,
            Kind: StockInKind.WarrantyReceive,
            Status: StockDocumentStatus.Approved,
            ProductId: 1,
            Quantity: 1m,
            SerialNumbers: Array.Empty<string>(),
            PostedByUserId: 4));

        Assert.Single(store.Ledgers);
        Assert.Equal("StockIn", store.Ledgers[0].SourceDocumentType);
    }

    private static InMemoryInventoryStore CreateStore()
    {
        var store = new InMemoryInventoryStore();
        store.Products[1] = new ProductSnapshot(1, false);
        return store;
    }

    private static PostStockInCommand CreateCommand(StockDocumentStatus status) => new(
        DocumentId: 1,
        WarehouseId: 1,
        Kind: StockInKind.Purchase,
        Status: status,
        ProductId: 1,
        Quantity: 1m,
        SerialNumbers: Array.Empty<string>(),
        PostedByUserId: 1);
}
