using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using Xunit;

namespace QuanLyHangHoa.Tests.Inventory;

public class EfInventoryUnitOfWorkTests
{
    [Fact]
    public void PostOpeningBalance_persists_balance_ledger_audit_and_serials_in_sqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        context.Database.EnsureCreated();
        context.Products.Add(new Product
        {
            Id = 100,
            Name = "Serial product",
            CategoryId = 1,
            BrandId = 1,
            UnitId = 1,
            Quantity = 999,
            UnitPrice = 10m,
            IsSerialManaged = true
        });
        context.SaveChanges();

        var postedAt = new DateTime(2026, 4, 27, 9, 0, 0);
        var documentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var service = new InventoryPostingService(
            new EfInventoryUnitOfWork(context),
            new TestWarehouseProvider(1),
            new TestClock(postedAt));

        service.PostStockIn(new PostStockInCommand(
            documentId,
            StockInKind.OpeningBalance,
            StockDocumentStatus.Approved,
            100,
            2,
            new[] { "EF-SN-001", "EF-SN-002" },
            7));

        var balance = Assert.Single(context.StockBalances);
        Assert.Equal(100, balance.ProductId);
        Assert.Equal(1, balance.WarehouseId);
        Assert.Equal(2, balance.OnHandQuantity);
        Assert.Equal(2, balance.AvailableQuantity);
        Assert.Equal(0, balance.ReservedQuantity);
        Assert.Equal(999, context.Products.Single(p => p.Id == 100).Quantity);

        var serials = context.ProductSerials.Where(s => s.ProductId == 100).OrderBy(s => s.SerialNumber).ToList();
        Assert.Equal(2, serials.Count);
        Assert.All(serials, serial =>
        {
            Assert.Equal(SerialStatus.InStock.ToString(), serial.Status);
            Assert.Equal(1, serial.CurrentWarehouseId);
        });

        var ledger = Assert.Single(context.StockLedgers);
        Assert.Equal(documentId, ledger.DocumentId);
        Assert.Equal(StockLedgerDirection.In.ToString(), ledger.Direction);
        Assert.Equal(2, ledger.Quantity);
        Assert.Equal(postedAt, ledger.PostedAt);
        Assert.Equal(7, ledger.PostedByUserId);

        var audit = Assert.Single(context.AuditLogs);
        Assert.Equal(documentId, audit.DocumentId);
        Assert.Equal(AuditActionCode.PostStockIn.ToString(), audit.ActionCode);
        Assert.Equal(postedAt, audit.PerformedAt);
        Assert.Equal(7, audit.PerformedByUserId);
    }

    [Fact]
    public void PostStockOut_with_missing_balance_does_not_persist_any_rows_in_sqlite()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        context.Database.EnsureCreated();
        context.Products.Add(new Product
        {
            Id = 101,
            Name = "Non serial product",
            CategoryId = 1,
            BrandId = 1,
            UnitId = 1,
            Quantity = 999,
            UnitPrice = 10m,
            IsSerialManaged = false
        });
        context.SaveChanges();

        var service = new InventoryPostingService(
            new EfInventoryUnitOfWork(context),
            new TestWarehouseProvider(1),
            new TestClock(new DateTime(2026, 4, 27, 10, 0, 0)));

        var exception = Assert.Throws<InventoryDomainException>(() => service.PostStockOut(
            new PostStockOutCommand(
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                StockOutKind.Sale,
                StockDocumentStatus.Approved,
                101,
                1,
                Array.Empty<string>(),
                7)));

        Assert.Equal("Insufficient available stock.", exception.Message);
        Assert.Empty(context.StockBalances);
        Assert.Empty(context.StockLedgers);
        Assert.Empty(context.AuditLogs);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestWarehouseProvider : IDefaultWarehouseProvider
    {
        private readonly int _warehouseId;

        public TestWarehouseProvider(int warehouseId)
        {
            _warehouseId = warehouseId;
        }

        public int GetDefaultWarehouseId()
        {
            return _warehouseId;
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime now)
        {
            Now = now;
        }

        public DateTime Now { get; }
    }
}
