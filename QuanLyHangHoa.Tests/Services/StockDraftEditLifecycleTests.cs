using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class StockDraftEditLifecycleTests
{
    [Fact]
    public async Task StockIn_rejects_detail_edits_after_submission()
    {
        using var connection = OpenDatabase();
        var document = new StockIn
        {
            DocumentCode = "SI-PENDING-EDIT",
            WarehouseId = 1,
            ImportDate = DateTime.UtcNow,
            PurposeCode = "Purchase",
            Status = "PendingApproval",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        Add(connection, db => db.StockIns.Add(document));

        var service = new StockInService(() => DatabaseHelper.CreateContext(connection));

        await AssertPendingEditRejectedAsync(() => service.SaveDraftAsync(
            document, new List<StockInLine>(), 1, Guid.NewGuid()));
    }

    [Fact]
    public async Task StockOut_rejects_detail_edits_after_submission()
    {
        using var connection = OpenDatabase();
        var document = new StockOut
        {
            DocumentCode = "SO-PENDING-EDIT",
            WarehouseId = 1,
            CustomerId = 1,
            ExportDate = DateTime.UtcNow,
            PurposeCode = "Sale",
            Status = "PendingApproval",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        Add(connection, db => db.StockOuts.Add(document));

        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));

        await AssertPendingEditRejectedAsync(() => service.SaveDraftAsync(
            document, new List<StockOutLine>(), 1, Guid.NewGuid()));
    }

    [Fact]
    public async Task Transfer_rejects_detail_edits_after_submission()
    {
        using var connection = OpenDatabase();
        var document = new StockTransfer
        {
            DocumentCode = "ST-PENDING-EDIT",
            FromWarehouseId = 1,
            ToWarehouseId = 2,
            TransferDate = DateTime.UtcNow,
            Status = "PendingApproval",
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        Add(connection, db => db.StockTransfers.Add(document));

        var service = new StockTransferService(() => DatabaseHelper.CreateContext(connection));

        await AssertPendingEditRejectedAsync(() => service.SaveDraftAsync(
            document, new List<StockTransferLine>(), 1, Guid.NewGuid()));
    }

    [Fact]
    public async Task Adjustment_rejects_detail_edits_after_submission()
    {
        using var connection = OpenDatabase();
        var document = new StockAdjustment
        {
            DocumentCode = "ADJ-PENDING-EDIT",
            WarehouseId = 1,
            AdjustmentType = "Manual",
            ReasonCode = "TEST",
            Status = "PendingApproval",
            CreatedBy = 1
        };
        Add(connection, db => db.StockAdjustments.Add(document));

        var service = new StockAdjustmentService(() => DatabaseHelper.CreateContext(connection));

        await AssertPendingEditRejectedAsync(() => service.SaveDraftAsync(
            document, new List<StockAdjustmentLine>(), 1, Guid.NewGuid()));
    }

    private static async Task AssertPendingEditRejectedAsync(Func<Task> edit)
    {
        var error = await Assert.ThrowsAsync<InventoryDomainException>(edit);
        Assert.Equal("Only draft documents can be edited.", error.Message);
    }

    private static void Add(SqliteConnection connection, Action<AppDbContext> add)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        add(db);
        db.SaveChanges();
    }

    private static SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Warehouses.Add(new Warehouse
        {
            Id = 2,
            WarehouseCode = "WH2",
            DisplayName = "Second warehouse",
            IsActive = true
        });
        db.SaveChanges();
        return connection;
    }
}
