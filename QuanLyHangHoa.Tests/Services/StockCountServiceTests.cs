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

        service.CreateSession(session);

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

        var adjustment = assertContext.StockAdjustments
            .Include(a => a.Lines)
            .Single(a => a.ReferenceDocumentId == sessionId && a.ReferenceDocumentType == "StockCountSession");
            
        Assert.Equal("StockCount", adjustment.AdjustmentType);
        Assert.Equal("đã ghi sổ", adjustment.Status);
        
        Assert.NotNull(adjustment.Lines);
        var line = Assert.Single(adjustment.Lines);
        Assert.Equal(600, line.ProductId);
    }
}
