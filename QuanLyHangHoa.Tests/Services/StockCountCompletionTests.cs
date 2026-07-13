using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public sealed class StockCountCompletionTests
{
    [Fact]
    public void Completion_posts_linked_corrections_updates_balance_and_is_idempotent()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        int lineId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.AppUsers.Find(1)!.RoleCode = "Quản lý";
            db.Products.Add(new Product
            {
                Id = 1800,
                ProductCode = "COUNT-CORRECTION",
                DisplayName = "Count correction product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true,
                IsSerialTracked = false
            });
            db.StockBalances.Add(new StockBalance
            {
                ProductId = 1800,
                WarehouseId = 1,
                OnHandQuantity = 5m,
                AvailableQuantity = 5m
            });
            var session = new StockCountSession
            {
                SessionCode = "COUNT-COMPLETE-001",
                WarehouseId = 1,
                Status = "đã kiểm kê",
                CountDate = DateTime.UtcNow,
                CreatedBy = 1,
                Lines = new List<StockCountLine>
                {
                    new()
                    {
                        ProductId = 1800,
                        SystemQuantity = 5m,
                        CountedQuantity = 7m,
                        VarianceQuantity = 2m
                    }
                }
            };
            db.StockCountSessions.Add(session);
            db.SaveChanges();
            sessionId = session.Id;
            lineId = session.Lines.Single().Id;
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        service.ProcessResults(sessionId, 1);
        service.ProcessResults(sessionId, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var sessionAfter = assertContext.StockCountSessions.Single(item => item.Id == sessionId);
        Assert.Equal("hoàn thành", sessionAfter.Status);
        var correction = Assert.Single(assertContext.StockIns.Include(item => item.Lines));
        Assert.Equal("Posted", correction.Status);
        Assert.Equal(sessionId, correction.StockCountSessionId);
        Assert.Equal(lineId, correction.StockCountLineId);
        Assert.Equal(1, correction.ApprovedBy);
        Assert.NotNull(correction.ApprovedAt);
        Assert.Equal(2m, correction.Lines.Single().BaseQuantity);

        var balance = assertContext.StockBalances.Single(item =>
            item.ProductId == 1800 && item.WarehouseId == 1);
        Assert.Equal(7m, balance.OnHandQuantity);
        Assert.Equal(7m, balance.AvailableQuantity);
        Assert.Single(assertContext.StockLedgers.Where(item =>
            item.SourceDocumentType == "StockIn" && item.SourceDocumentId == correction.Id));
    }

    [Fact]
    public void Completion_posts_negative_serial_adjustment_and_marks_serial_inactive()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        int lineId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.AppUsers.Find(1)!.RoleCode = "Quản lý";
            db.Products.Add(new Product
            {
                Id = 1801,
                ProductCode = "COUNT-SERIAL-OUT",
                DisplayName = "Count serial out product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true,
                IsSerialTracked = true
            });
            db.StockBalances.Add(new StockBalance
            {
                ProductId = 1801,
                WarehouseId = 1,
                OnHandQuantity = 2m,
                AvailableQuantity = 2m
            });
            db.ProductSerials.Add(new ProductSerial
            {
                ProductId = 1801,
                SerialNumber = "COUNT-SN-OUT-1",
                CurrentWarehouseId = 1,
                CurrentStatus = "InStock",
                LastStockInLineId = 0
            });
            var session = new StockCountSession
            {
                SessionCode = "COUNT-SERIAL-OUT-001",
                WarehouseId = 1,
                Status = "đã kiểm kê",
                CountDate = DateTime.UtcNow,
                CreatedBy = 1,
                Lines = new List<StockCountLine>
                {
                    new()
                    {
                        ProductId = 1801,
                        SystemQuantity = 2m,
                        CountedQuantity = 1m,
                        VarianceQuantity = -1m,
                        SerialNumbers = "COUNT-SN-OUT-1"
                    }
                }
            };
            db.StockCountSessions.Add(session);
            db.SaveChanges();
            sessionId = session.Id;
            lineId = session.Lines.Single().Id;
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        service.ProcessResults(sessionId, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var correction = Assert.Single(assertContext.StockOuts.Include(item => item.Lines));
        Assert.Equal("Posted", correction.Status);
        Assert.Equal("Adjustment", correction.PurposeCode);
        Assert.Equal(sessionId, correction.StockCountSessionId);
        Assert.Equal(lineId, correction.StockCountLineId);
        var balance = assertContext.StockBalances.Single(item =>
            item.ProductId == 1801 && item.WarehouseId == 1);
        Assert.Equal(1m, balance.OnHandQuantity);
        Assert.Equal(1m, balance.AvailableQuantity);
        var serial = assertContext.ProductSerials.Single(item => item.SerialNumber == "COUNT-SN-OUT-1");
        Assert.Equal("Inactive", serial.CurrentStatus);
        Assert.Equal(correction.Lines.Single().Id, serial.LastStockOutLineId);
    }

    [Fact]
    public void Completion_rolls_back_all_corrections_when_any_line_cannot_post()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.AppUsers.Find(1)!.RoleCode = "Quản lý";
            db.Products.AddRange(
                new Product
                {
                    Id = 1802,
                    ProductCode = "COUNT-ROLLBACK-IN",
                    DisplayName = "Count rollback in product",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsActive = true
                },
                new Product
                {
                    Id = 1803,
                    ProductCode = "COUNT-ROLLBACK-OUT",
                    DisplayName = "Count rollback out product",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsActive = true
                });
            db.StockBalances.AddRange(
                new StockBalance
                {
                    ProductId = 1802,
                    WarehouseId = 1,
                    OnHandQuantity = 5m,
                    AvailableQuantity = 5m
                },
                new StockBalance
                {
                    ProductId = 1803,
                    WarehouseId = 1,
                    OnHandQuantity = 0m,
                    AvailableQuantity = 0m
                });
            var session = new StockCountSession
            {
                SessionCode = "COUNT-ROLLBACK-001",
                WarehouseId = 1,
                Status = "đã kiểm kê",
                CountDate = DateTime.UtcNow,
                CreatedBy = 1,
                Lines = new List<StockCountLine>
                {
                    new()
                    {
                        ProductId = 1802,
                        SystemQuantity = 5m,
                        CountedQuantity = 7m,
                        VarianceQuantity = 2m
                    },
                    new()
                    {
                        ProductId = 1803,
                        SystemQuantity = 1m,
                        CountedQuantity = 0m,
                        VarianceQuantity = -1m
                    }
                }
            };
            db.StockCountSessions.Add(session);
            db.SaveChanges();
            sessionId = session.Id;
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        Assert.Throws<InventoryDomainException>(() => service.ProcessResults(sessionId, 1));

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Equal("đã kiểm kê", assertContext.StockCountSessions.Find(sessionId)!.Status);
        Assert.Empty(assertContext.StockIns.Where(item => item.StockCountSessionId == sessionId));
        Assert.Empty(assertContext.StockOuts.Where(item => item.StockCountSessionId == sessionId));
        Assert.Equal(5m, assertContext.StockBalances.Single(item => item.ProductId == 1802).OnHandQuantity);
        Assert.Equal(0m, assertContext.StockBalances.Single(item => item.ProductId == 1803).OnHandQuantity);
    }
}
