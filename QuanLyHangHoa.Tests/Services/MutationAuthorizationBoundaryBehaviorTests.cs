using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class MutationAuthorizationBoundaryBehaviorTests
{
    private const int ActorId = 2;
    private const string UnauthorizedMessage = "The current user is not authorized for this action.";

    [Fact]
    public void SavePurchaseInvoice_rejects_stale_actor_without_writes()
    {
        using var connection = CreateDatabase();
        RevokeRole(connection);
        var service = new InvoiceService(() => CreateContext(connection));
        var invoice = new PurchaseInvoice
        {
            InvoiceCode = "PI-AUTH-001",
            SupplierId = 1,
            InvoiceDate = DateTime.UtcNow,
            CreatedBy = ActorId,
            Lines = new List<PurchaseInvoiceLine>
            {
                new() { ProductId = 100, UnitId = 1, Quantity = 1, UnitPrice = 10m }
            }
        };

        AssertUnauthorized(() => service.SavePurchaseInvoice(invoice, ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.PurchaseInvoices);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void StockIn_SaveDraft_rejects_inactive_stale_actor_without_writes()
    {
        using var connection = CreateDatabase();
        DeactivateActor(connection);
        var service = new StockInService(() => CreateContext(connection));

        AssertUnauthorized(() => service.SaveDraft(
            new StockIn { DocumentCode = "SI-AUTH-001", WarehouseId = 1, PurposeCode = "Purchase" },
            new List<StockInLine>(),
            ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.StockIns);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void StockOut_SaveDraft_rejects_stale_actor_without_writes()
    {
        using var connection = CreateDatabase();
        RevokeRole(connection);
        var service = new StockOutService(() => CreateContext(connection));

        AssertUnauthorized(() => service.SaveDraft(
            new StockOut
            {
                DocumentCode = "SO-AUTH-001",
                CustomerId = 1,
                WarehouseId = 1,
                PurposeCode = "Sale"
            },
            new List<StockOutLine>(),
            ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.StockOuts);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void StockTransfer_SaveDraft_rejects_inactive_stale_actor_without_writes()
    {
        using var connection = CreateDatabase();
        DeactivateActor(connection);
        var service = new StockTransferService(() => CreateContext(connection));

        AssertUnauthorized(() => service.SaveDraft(
            new StockTransfer
            {
                DocumentCode = "ST-AUTH-001",
                FromWarehouseId = 1,
                ToWarehouseId = 2
            },
            new List<StockTransferLine>(),
            ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.StockTransfers);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void StockAdjustment_SaveDraft_rejects_stale_actor_without_writes()
    {
        using var connection = CreateDatabase();
        RevokeRole(connection);
        var service = new StockAdjustmentService(() => CreateContext(connection));

        AssertUnauthorized(() => service.SaveDraft(
            new StockAdjustment
            {
                DocumentCode = "ADJ-AUTH-001",
                WarehouseId = 1,
                AdjustmentType = "Manual",
                ReasonCode = "TEST"
            },
            new List<StockAdjustmentLine>(),
            ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.StockAdjustments);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void StockCount_CreateSession_rejects_inactive_stale_actor_without_writes()
    {
        using var connection = CreateDatabase();
        DeactivateActor(connection);
        var service = new StockCountService(() => CreateContext(connection));
        var session = new StockCountSession
        {
            SessionCode = "COUNT-AUTH-001",
            WarehouseId = 1,
            CountDate = DateTime.UtcNow,
            Status = "Draft",
            CreatedBy = ActorId
        };

        AssertUnauthorized(() => service.CreateSession(session, ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.StockCountSessions);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public void StockReversal_rejects_stale_actor_before_source_lookup_or_writes()
    {
        using var connection = CreateDatabase();
        RevokeRole(connection);
        var service = new StockReversalService(() => CreateContext(connection));

        AssertUnauthorized(() => service.ReverseDocument("StockIn", 999, ActorId));

        using var db = CreateContext(connection);
        Assert.Empty(db.StockAdjustments);
        Assert.Empty(db.AuditLogs);
    }

    private static SqliteConnection CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Products.Add(new Product
        {
            Id = 100,
            ProductCode = "P100",
            DisplayName = "Authorization product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true
        });
        db.SaveChanges();
        return connection;
    }

    private static void RevokeRole(SqliteConnection connection)
    {
        using var db = CreateContext(connection);
        db.AppUsers.Single(user => user.Id == ActorId).RoleCode = "Nh\u00e2n vi\u00ean b\u1ea3o h\u00e0nh";
        db.SaveChanges();
    }

    private static void DeactivateActor(SqliteConnection connection)
    {
        using var db = CreateContext(connection);
        db.AppUsers.Single(user => user.Id == ActorId).IsActive = false;
        db.SaveChanges();
    }

    private static void AssertUnauthorized(Action action)
    {
        var error = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal(UnauthorizedMessage, error.Message);
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
