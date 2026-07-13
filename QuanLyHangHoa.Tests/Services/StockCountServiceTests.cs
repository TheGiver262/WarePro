using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

namespace QuanLyHangHoa.Tests.Services;

public class StockCountServiceTests
{
    [Fact]
    public void CreateSession_saves_to_database()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setupContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(setupContext);
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        var session = new StockCountSession
        {
            SessionCode = "CNT-001",
            WarehouseId = 1,
            CountDate = DateTime.UtcNow,
            CreatedBy = 1,
            Status = "Draft"
        };

        service.CreateSession(session, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var saved = assertContext.StockCountSessions.Single();
        Assert.Equal("CNT-001", saved.SessionCode);
    }

    [Fact]
    public void ProcessResults_creates_adjustment_for_variances()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Find(1)!.RoleCode = "Quản lý";
            seedContext.Products.Add(new Product { 
                Id = 600, 
                ProductCode = "P600", 
                DisplayName = "Count product", 
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 10m,
                IsActive = true
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 600,
                WarehouseId = 1,
                OnHandQuantity = 5,
                AvailableQuantity = 5
            });
            
            var session = new StockCountSession
            {
                SessionCode = "CNT-002",
                WarehouseId = 1,
                Status = "đã kiểm kê",
                CountDate = DateTime.UtcNow,
                CreatedBy = 1,
                Lines = new List<StockCountLine>
                {
                    new StockCountLine
                    {
                        ProductId = 600,
                        SystemQuantity = 5,
                        CountedQuantity = 7,
                        VarianceQuantity = 2
                    }
                }
            };
            seedContext.StockCountSessions.Add(session);
            seedContext.SaveChanges();
            sessionId = session.Id;
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        service.ProcessResults(sessionId, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var sessionAfter = assertContext.StockCountSessions.Find(sessionId);
        Assert.NotNull(sessionAfter);
        Assert.Equal("hoàn thành", sessionAfter.Status);

        var stockIn = assertContext.StockIns
            .Include(si => si.Lines)
            .Single(si => si.DocumentCode.StartsWith("SI-ADJ-CNT-002"));
            
        Assert.Equal("Posted", stockIn.Status);
        Assert.Equal("Adjustment", stockIn.PurposeCode);
        Assert.Equal("Nhập để điều chỉnh tồn kho (Theo phiên kiểm kê CNT-002)", stockIn.Notes);
        
        Assert.NotNull(stockIn.Lines);
        var line = Assert.Single(stockIn.Lines);
        Assert.Equal(600, line.ProductId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void CreateSession_creates_audit_log()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setupContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(setupContext);
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        var session = new StockCountSession
        {
            SessionCode = "CNT-AUD-001",
            WarehouseId = 1,
            CountDate = DateTime.UtcNow,
            CreatedBy = 1,
            Status = "đã kiểm kê"
        };

        service.CreateSession(session, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var logs = assertContext.AuditLogs.ToList();
        var audit = Assert.Single(logs);
        Assert.Equal("StockCountSession", audit.EntityName);
        Assert.Equal("CREATE", audit.ActionCode);
        Assert.Equal(session.Id, audit.EntityId);
        Assert.Contains("CNT-AUD-001", audit.AfterJson ?? "");
    }

    [Fact]
    public void ProcessResults_creates_audit_log()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Find(1)!.RoleCode = "Quản lý";
            seedContext.Products.Add(new Product { 
                Id = 700, 
                ProductCode = "P700", 
                DisplayName = "Audit count product", 
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 15m,
                IsActive = true
            });
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 700,
                WarehouseId = 1,
                OnHandQuantity = 10,
                AvailableQuantity = 10
            });
            
            var session = new StockCountSession
            {
                SessionCode = "CNT-AUD-002",
                WarehouseId = 1,
                Status = "đã kiểm kê",
                CountDate = DateTime.UtcNow,
                CreatedBy = 1,
                Lines = new List<StockCountLine>
                {
                    new StockCountLine
                    {
                        ProductId = 700,
                        SystemQuantity = 10,
                        CountedQuantity = 8,
                        VarianceQuantity = -2
                    }
                }
            };
            seedContext.StockCountSessions.Add(session);
            seedContext.SaveChanges();
            sessionId = session.Id;
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        service.ProcessResults(sessionId, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var logs = assertContext.AuditLogs.Where(l => l.EntityName == "StockCountSession" && l.ActionCode == "POST").ToList();
        var audit = Assert.Single(logs);
        Assert.Equal("StockCountSession", audit.EntityName);
        Assert.Equal("POST", audit.ActionCode);
        Assert.Equal(sessionId, audit.EntityId);
        Assert.Contains("hoàn thành", audit.AfterJson ?? "");
        Assert.Contains("đã kiểm kê", audit.BeforeJson ?? "");
    }

    [Fact]
    public void ProcessResults_posts_StockIn_and_updates_StockBalances_atomically()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int sessionId;
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Find(1)!.RoleCode = "Quản lý";
            seedContext.Products.Add(new Product { 
                Id = 800, 
                ProductCode = "P800", 
                DisplayName = "Balance test product", 
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 20m,
                IsActive = true
            });

            // Seed initial StockBalance with OnHandQuantity = 5
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 800,
                WarehouseId = 1,
                OnHandQuantity = 5,
                AvailableQuantity = 5,
                ReservedQuantity = 0
            });
            
            var session = new StockCountSession
            {
                SessionCode = "CNT-BAL-001",
                WarehouseId = 1,
                Status = "đã kiểm kê",
                CountDate = DateTime.UtcNow,
                CreatedBy = 1,
                Lines = new List<StockCountLine>
                {
                    new StockCountLine
                    {
                        ProductId = 800,
                        SystemQuantity = 5,
                        CountedQuantity = 7,
                        VarianceQuantity = 2
                    }
                }
            };
            seedContext.StockCountSessions.Add(session);
            seedContext.SaveChanges();
            sessionId = session.Id;
        }

        var service = new StockCountService(() => DatabaseHelper.CreateContext(connection));
        service.ProcessResults(sessionId, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var balance = assertContext.StockBalances.Single(b => b.ProductId == 800 && b.WarehouseId == 1);
        Assert.Equal(7, balance.OnHandQuantity);
        Assert.Equal(7, balance.AvailableQuantity);

        var stockIn = assertContext.StockIns
            .Include(si => si.Lines)
            .Single(si => si.DocumentCode.StartsWith("SI-ADJ-CNT-BAL-001"));
        Assert.Equal("Posted", stockIn.Status);
        Assert.Equal(2, stockIn.Lines.Single().Quantity);
    }
}
