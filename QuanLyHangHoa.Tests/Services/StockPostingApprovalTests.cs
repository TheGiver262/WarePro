using System;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class StockPostingApprovalTests
{
    [Fact]
    public void Transfer_cannot_post_directly_from_draft()
    {
        using var connection = OpenDatabase();
        int transferId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var transfer = new StockTransfer
            {
                DocumentCode = "ST-DRAFT-BLOCK",
                FromWarehouseId = 1,
                ToWarehouseId = 2,
                Status = "Draft",
                TransferDate = DateTime.UtcNow,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.StockTransfers.Add(transfer);
            db.SaveChanges();
            transferId = transfer.Id;
        }

        var service = new StockTransferService(() => DatabaseHelper.CreateContext(connection));

        var error = Assert.Throws<InventoryDomainException>(() => service.Post(transferId, 1));
        Assert.Equal("Only approved documents can be posted.", error.Message);
    }

    [Fact]
    public void Adjustment_cannot_post_directly_from_draft()
    {
        using var connection = OpenDatabase();
        int adjustmentId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var adjustment = new StockAdjustment
            {
                DocumentCode = "ADJ-DRAFT-BLOCK",
                WarehouseId = 1,
                AdjustmentType = "Manual",
                ReasonCode = "TEST",
                Status = "Draft",
                CreatedBy = 1
            };
            db.StockAdjustments.Add(adjustment);
            db.SaveChanges();
            adjustmentId = adjustment.Id;
        }

        var service = new StockAdjustmentService(() => DatabaseHelper.CreateContext(connection));

        var error = Assert.Throws<InventoryDomainException>(() => service.Post(adjustmentId, 1));
        Assert.Equal("Only approved documents can be posted.", error.Message);
    }

    [Fact]
    public void StockIn_cannot_post_directly_from_draft()
    {
        using var connection = OpenDatabase();
        int stockInId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var stockIn = new StockIn
            {
                DocumentCode = "SI-DRAFT-BLOCK",
                WarehouseId = 1,
                ImportDate = DateTime.UtcNow,
                PurposeCode = "Purchase",
                Status = "Draft",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.StockIns.Add(stockIn);
            db.SaveChanges();
            stockInId = stockIn.Id;
        }

        var service = new StockInService(() => DatabaseHelper.CreateContext(connection));

        var error = Assert.Throws<InventoryDomainException>(() => service.Post(stockInId, 1));
        Assert.Equal("Only approved documents can be posted.", error.Message);
    }

    [Fact]
    public void StockOut_cannot_post_directly_from_draft()
    {
        using var connection = OpenDatabase();
        int stockOutId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var stockOut = new StockOut
            {
                DocumentCode = "SO-DRAFT-BLOCK",
                CustomerId = 1,
                WarehouseId = 1,
                ExportDate = DateTime.UtcNow,
                PurposeCode = "Sale",
                Status = "Draft",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.StockOuts.Add(stockOut);
            db.SaveChanges();
            stockOutId = stockOut.Id;
        }

        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));

        var error = Assert.Throws<InventoryDomainException>(() => service.Post(stockOutId, 1));
        Assert.Equal("Only approved documents can be posted.", error.Message);
    }
    [Fact]
    public void Unauthorized_user_cannot_approve_transfer()
    {
        using var connection = OpenDatabase();
        int transferId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var transfer = new StockTransfer
            {
                DocumentCode = "ST-UNAUTHORIZED",
                FromWarehouseId = 1,
                ToWarehouseId = 2,
                Status = "PendingApproval",
                TransferDate = DateTime.UtcNow,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.StockTransfers.Add(transfer);
            db.SaveChanges();
            transferId = transfer.Id;
        }

        var service = new StockTransferService(() => DatabaseHelper.CreateContext(connection));

        var error = Assert.Throws<InventoryDomainException>(() => service.Approve(transferId, 3));
        Assert.Equal("You are not authorized to approve stock documents.", error.Message);
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
        db.AppUsers.Find(1)!.RoleCode = "Quản lý";
        db.SaveChanges();
        return connection;
    }
}
