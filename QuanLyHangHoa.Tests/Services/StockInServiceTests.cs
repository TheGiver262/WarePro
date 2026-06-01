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

public class StockInServiceTests
{
    [Fact]
    public void Create_posts_to_database_and_updates_inventory()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 200, ProductCode = "P200",
                DisplayName = "Service stock-in product",
                CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = false });
            seedContext.SaveChanges();
        }

        var service = new StockInService(() => CreateContext(connection));
        var stockIn = new StockIn
        {
            DocumentCode = "SI-001",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase",
            CreatedAt = new DateTime(2026, 4, 27, 11, 30, 0)
        };
        var lines = new List<StockInLine>
        {
            new StockInLine
            {
                ProductId = 200,
                UnitId = 1,
                Quantity = 3,
                UnitPrice = 12m
            }
        };

        service.SaveDraft(stockIn, lines, 1);
        service.Post(stockIn.Id, 1);

        using var assertContext = CreateContext(connection);
        var savedStockIn = assertContext.StockIns.Include(s => s.Lines).Single();
        Assert.Equal("SI-001", savedStockIn.DocumentCode);
        Assert.Equal(1, savedStockIn.CreatedBy);

        var balance = assertContext.StockBalances.Single(b => b.ProductId == 200);
        Assert.Equal(3, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 200);
        Assert.Equal("In", ledger.MovementType);
        Assert.Equal(3, ledger.Quantity);
    }

    [Fact]
    public void SaveDraft_allows_same_serials_but_post_validates_and_throws_correct_errors()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product 
            { 
                Id = 201, 
                ProductCode = "P201",
                DisplayName = "Serial Product",
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 20m, 
                IsSerialTracked = true 
            });
            seedContext.SaveChanges();
        }

        var service = new StockInService(() => CreateContext(connection));

        // Draft A
        var stockInA = new StockIn { DocumentCode = "SI-A", SupplierId = 1, WarehouseId = 1, PurposeCode = "Purchase" };
        var linesA = new List<StockInLine>
        {
            new StockInLine
            {
                ProductId = 201,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 20m,
                ProductSerials = new List<ProductSerial> { new ProductSerial { SerialNumber = "SN-001" } }
            }
        };

        // Draft B
        var stockInB = new StockIn { DocumentCode = "SI-B", SupplierId = 1, WarehouseId = 1, PurposeCode = "Purchase" };
        var linesB = new List<StockInLine>
        {
            new StockInLine
            {
                ProductId = 201,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 20m,
                ProductSerials = new List<ProductSerial> { new ProductSerial { SerialNumber = "SN-001" } }
            }
        };

        // 1. Both drafts should be saved successfully with the same serials
        service.SaveDraft(stockInA, linesA, 1);
        service.SaveDraft(stockInB, linesB, 1);

        using (var db = CreateContext(connection))
        {
            Assert.Contains(db.StockInLines, l => l.DraftSerials == "SN-001");
            Assert.Equal(2, db.StockInLines.Count(l => l.DraftSerials == "SN-001"));
        }

        // 2. Post Draft A -> should succeed and insert the serial "SN-001"
        service.Post(stockInA.Id, 1);

        using (var db = CreateContext(connection))
        {
            var postedA = db.StockIns.Find(stockInA.Id);
            Assert.Equal(DocumentStatus.Posted, postedA.Status);
            Assert.True(db.ProductSerials.Any(ps => ps.SerialNumber == "SN-001"));
        }

        // 3. Post Draft B -> should fail because "SN-001" already exists in DB
        var ex = Assert.Throws<Exception>(() => service.Post(stockInB.Id, 1));
        Assert.Equal("Số serial [SN-001] đã tồn tại trong hệ thống. Vui lòng kiểm tra và chỉnh sửa lại phiếu nháp trước khi duyệt.", ex.Message);

        // 4. Test document-level duplicate serials: Draft C with duplicate serials in the same document
        var stockInC = new StockIn { DocumentCode = "SI-C", SupplierId = 1, WarehouseId = 1, PurposeCode = "Purchase" };
        var linesC = new List<StockInLine>
        {
            new StockInLine
            {
                ProductId = 201,
                UnitId = 1,
                Quantity = 2,
                UnitPrice = 20m,
                ProductSerials = new List<ProductSerial> 
                { 
                    new ProductSerial { SerialNumber = "SN-002" },
                    new ProductSerial { SerialNumber = "SN-002" }
                }
            }
        };
        service.SaveDraft(stockInC, linesC, 1);

        var exDup = Assert.Throws<Exception>(() => service.Post(stockInC.Id, 1));
        Assert.Equal("Các số serial sau bị trùng lặp trong phiếu: [SN-002]. Vui lòng kiểm tra lại trước khi duyệt.", exDup.Message);
    }

    [Fact]
    public void Post_updates_inventory_using_BaseQuantity_when_unit_conversion_is_applied()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            seedContext.Products.Add(new Product 
            { 
                Id = 300, 
                ProductCode = "P300",
                DisplayName = "Converted Unit Product",
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 10m, 
                IsSerialTracked = false 
            });
            seedContext.ProductUnits.Add(new ProductUnit
            {
                ProductId = 300,
                UnitId = 2,
                ConversionFactor = 12m,
                IsBaseUnit = false,
                IsPurchaseUnit = true,
                IsSalesUnit = true
            });
            seedContext.SaveChanges();
        }

        var service = new StockInService(() => CreateContext(connection));
        var stockIn = new StockIn
        {
            DocumentCode = "SI-CONV",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase",
            CreatedAt = DateTime.Now
        };
        var lines = new List<StockInLine>
        {
            new StockInLine
            {
                ProductId = 300,
                UnitId = 2,
                Quantity = 2,
                UnitPrice = 100m
            }
        };

        service.SaveDraft(stockIn, lines, 1);
        service.Post(stockIn.Id, 1);

        using var assertContext = CreateContext(connection);
        var balance = assertContext.StockBalances.Single(b => b.ProductId == 300);
        // Xác minh tồn kho tăng theo BaseQuantity (2 * 12 = 24) chứ không phải Quantity giao dịch (2)
        Assert.Equal(24, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 300);
        Assert.Equal("In", ledger.MovementType);
        Assert.Equal(24, ledger.Quantity);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }
}
