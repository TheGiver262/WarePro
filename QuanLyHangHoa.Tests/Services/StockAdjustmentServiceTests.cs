using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;
using System.Collections.Generic;
using System;

namespace QuanLyHangHoa.Tests.Services;

public class StockAdjustmentServiceTests
{
    [Fact]
    public async Task SaveDraft_allocates_document_code_when_new_document_code_is_blank()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
        }
        var service = new StockAdjustmentService(() => DatabaseHelper.CreateContext(connection));
        var document = new StockAdjustment
        {
            DocumentCode = string.Empty,
            WarehouseId = 1,
            AdjustmentType = "Increase",
            ReasonCode = "Manual"
        };

        await service.SaveDraftAsync(document, [], 1, Guid.NewGuid());

        Assert.Matches("^ADJ-[0-9]{8}-[0-9]{6}$", document.DocumentCode);
    }

    [Fact]
    public void Post_saves_adjustment_and_updates_stock_balance_ledger_and_audit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Find(1)!.RoleCode = "Quản lý";
            seedContext.Products.Add(new Product { Id = 500, ProductCode = "P500",
                DisplayName = "Adjustment service product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = false
                 });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 500,
                WarehouseId = 1,
                OnHandQuantity = 5,
                AvailableQuantity = 5
            });
            seedContext.SaveChanges();
        }

        var service = new StockAdjustmentService(() => DatabaseHelper.CreateContext(connection));
        var adjustment = new StockAdjustment
        {
            DocumentCode = "ADJ-SVC-001",
            WarehouseId = 1,
            AdjustmentType = "Manual",
            Status = StockDocumentStatus.Approved.ToString(),
            ReferenceDocumentCode = "COUNT-SVC-001",
            ReasonCode = "CountDifference",
            CreatedBy = 1,
            PostedBy = 1,
            Lines = new List<StockAdjustmentLine>()
        };
        
        adjustment.Lines.Add(new StockAdjustmentLine
        {
            ProductId = 500,
            QuantityDelta = -2m,
            BaseQuantityDelta = -2m,
            Direction = "Out"
        });

        service.SaveDraft(adjustment, adjustment.Lines.ToList(), 1);
        service.SubmitForApproval(adjustment.Id, 1);
        service.Approve(adjustment.Id, 1);
        service.Post(adjustment.Id, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var savedAdjustment = Assert.Single(assertContext.StockAdjustments.Include(a => a.Lines));
        Assert.Equal(StockDocumentStatus.Posted.ToString(), savedAdjustment.Status);
        Assert.NotNull(savedAdjustment.PostedAt);
        Assert.NotNull(savedAdjustment.Lines);
        Assert.Single(savedAdjustment.Lines);

        var balance = Assert.Single(assertContext.StockBalances);
        Assert.Equal(3, balance.OnHandQuantity);
        Assert.Equal(3, balance.AvailableQuantity);

        var ledger = Assert.Single(assertContext.StockLedgers);
        Assert.Equal("Out", ledger.MovementType);
        Assert.Equal(2, (int)ledger.Quantity);

        var audit = Assert.Single(assertContext.AuditLogs);
        Assert.Equal(AuditActionCode.PostStockAdjustment.ToString(), audit.ActionCode);
    }

    [Fact]
    public void Post_uses_all_draft_serials_for_serial_tracked_adjustment()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Find(1)!.RoleCode = "Quản lý";
            seedContext.Products.Add(new Product
            {
                Id = 501,
                ProductCode = "P501-SERIAL",
                DisplayName = "Adjustment serial product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = true
            });
            seedContext.SaveChanges();
        }

        var service = new StockAdjustmentService(() => DatabaseHelper.CreateContext(connection));
        var adjustment = new StockAdjustment
        {
            DocumentCode = "ADJ-SVC-SERIAL-001",
            WarehouseId = 1,
            AdjustmentType = "Manual",
            ReasonCode = "INVENTORY_COUNT",
            CreatedBy = 1
        };
        var lines = new List<StockAdjustmentLine>
        {
            new()
            {
                ProductId = 501,
                QuantityDelta = 2m,
                BaseQuantityDelta = 2m,
                Direction = "In",
                DraftSerials = "ADJ-SVC-SN-001,ADJ-SVC-SN-002"
            }
        };

        service.SaveDraft(adjustment, lines, 1);
        service.SubmitForApproval(adjustment.Id, 1);
        service.Approve(adjustment.Id, 1);
        service.Post(adjustment.Id, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var serials = assertContext.ProductSerials
            .Where(item => item.ProductId == 501)
            .OrderBy(item => item.SerialNumber)
            .ToList();
        Assert.Equal(2, serials.Count);
        Assert.All(serials, item =>
        {
            Assert.Equal("InStock", item.CurrentStatus);
            Assert.Equal(1, item.CurrentWarehouseId);
        });
    }
}
