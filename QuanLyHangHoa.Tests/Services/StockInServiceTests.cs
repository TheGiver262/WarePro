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
    public void List_stats_and_filters_preserve_all_lifecycle_states()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            var now = DateTime.UtcNow;
            db.StockIns.AddRange(
                CreateLifecycleDocument("LIFE-IN-D", DocumentStatus.Draft, now),
                CreateLifecycleDocument("LIFE-IN-P", DocumentStatus.PendingApproval, now),
                CreateLifecycleDocument("LIFE-IN-A", DocumentStatus.Approved, now),
                CreateLifecycleDocument("LIFE-IN-X", DocumentStatus.Posted, now));
            db.SaveChanges();
        }
        var service = new StockInService(() => CreateContext(connection));

        var stats = service.GetStockInStats("LIFE-IN", "", null, null, null, "Tất cả");

        Assert.Equal(4, stats.TotalCount);
        Assert.Equal(1, stats.DraftCount);
        Assert.Equal(1, stats.PendingApprovalCount);
        Assert.Equal(1, stats.ApprovedCount);
        Assert.Equal(1, stats.PostedCount);
        Assert.Equal(DocumentStatus.Draft, Assert.Single(service.GetStockInPaged("LIFE-IN", "", null, null, null, "Phiếu nháp", 0, 10)).Status);
        Assert.Equal(DocumentStatus.PendingApproval, Assert.Single(service.GetStockInPaged("LIFE-IN", "", null, null, null, "Chờ duyệt", 0, 10)).Status);
        Assert.Equal(DocumentStatus.Approved, Assert.Single(service.GetStockInPaged("LIFE-IN", "", null, null, null, "Đã duyệt", 0, 10)).Status);
        Assert.Equal(DocumentStatus.Posted, Assert.Single(service.GetStockInPaged("LIFE-IN", "", null, null, null, "Đã ghi sổ", 0, 10)).Status);
    }

    private static StockIn CreateLifecycleDocument(string code, string status, DateTime now) => new()
    {
        DocumentCode = code,
        SupplierId = 1,
        WarehouseId = 1,
        PurposeCode = "Purchase",
        Status = status,
        ImportDate = now,
        CreatedBy = 1,
        CreatedAt = now
    };

    [Fact]
    public async Task SaveDraft_allocates_document_code_when_new_document_code_is_blank()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
        }
        var service = new StockInService(() => CreateContext(connection));
        var document = new StockIn
        {
            DocumentCode = string.Empty,
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase"
        };

        await service.SaveDraftAsync(document, [], 1, Guid.NewGuid());

        Assert.Matches("^IN-[0-9]{8}-[0-9]{6}$", document.DocumentCode);
    }

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
            seedContext.ProductUnits.Add(new ProductUnit { ProductId = 200, UnitId = 1, ConversionFactor = 1m, IsBaseUnit = true });
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
        ApproveForPosting(service, connection, stockIn.Id);
        service.Post(stockIn.Id, 1);

        var stats = service.GetStockInStats(string.Empty, string.Empty, null, null, null, string.Empty);
        Assert.Equal(1, stats.TotalCount);
        Assert.Equal(0, stats.DraftCount);
        Assert.Equal(1, stats.PostedCount);

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
            seedContext.ProductUnits.Add(new ProductUnit { ProductId = 201, UnitId = 1, ConversionFactor = 1m, IsBaseUnit = true });
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
        ApproveForPosting(service, connection, stockInA.Id);
        service.Post(stockInA.Id, 1);

        using (var db = CreateContext(connection))
        {
            var postedA = db.StockIns.Find(stockInA.Id);
            Assert.NotNull(postedA);
            Assert.Equal(DocumentStatus.Posted, postedA.Status);
            Assert.True(db.ProductSerials.Any(ps => ps.SerialNumber == "SN-001"));
        }

        // 3. Post Draft B -> should fail because "SN-001" already exists in DB
        ApproveForPosting(service, connection, stockInB.Id);
        var ex = Assert.Throws<InventoryDomainException>(() => service.Post(stockInB.Id, 1));
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

        var exDup = Assert.Throws<InventoryDomainException>(() =>
            ApproveForPosting(service, connection, stockInC.Id));
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
        ApproveForPosting(service, connection, stockIn.Id);
        service.Post(stockIn.Id, 1);

        using var assertContext = CreateContext(connection);
        var balance = assertContext.StockBalances.Single(b => b.ProductId == 300);
        // Xác minh tồn kho tăng theo BaseQuantity (2 * 12 = 24) chứ không phải Quantity giao dịch (2)
        Assert.Equal(24, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 300);
        Assert.Equal("In", ledger.MovementType);
        Assert.Equal(24, ledger.Quantity);
    }

    [Fact]
    public async Task SubmitForApproval_rejects_invalid_serial_draft_and_keeps_status_draft()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product
            {
                Id = 450,
                ProductCode = "P450",
                DisplayName = "Submit validation product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = true
            });
            seedContext.ProductUnits.Add(new ProductUnit
            {
                ProductId = 450,
                UnitId = 1,
                ConversionFactor = 1m,
                IsBaseUnit = true,
                IsPurchaseUnit = true,
                IsSalesUnit = true
            });
            seedContext.SaveChanges();
        }

        var service = new StockInService(() => CreateContext(connection));
        var document = new StockIn
        {
            DocumentCode = "SI-INVALID-SUBMIT",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase"
        };
        await service.SaveDraftAsync(document,
        [
            new StockInLine
            {
                ProductId = 450,
                UnitId = 1,
                Quantity = 2m,
                ProductSerials = [new ProductSerial { SerialNumber = "ONLY-ONE" }]
            }
        ], 1, Guid.NewGuid());

        await Assert.ThrowsAsync<InventoryDomainException>(() => service.SubmitForApprovalAsync(
            document.Id, document.RowVersion, 1, Guid.NewGuid()));

        using var assertContext = CreateContext(connection);
        Assert.Equal(DocumentStatus.Draft, assertContext.StockIns.Single(item => item.Id == document.Id).Status);
    }

    [Fact]
    public void Post_query_count_does_not_grow_per_line()
    {
        var singleLineCount = CountPostSelects(1);
        var sixLineCount = CountPostSelects(6);

        Assert.True(
            sixLineCount <= singleLineCount + 2,
            $"Expected at most {singleLineCount + 2} SELECTs, but observed {sixLineCount}.");
    }

    private static int CountPostSelects(int lineCount)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.AddRange(Enumerable.Range(0, lineCount).Select(index => new Product
            {
                Id = 500 + index,
                ProductCode = $"N1-IN-{index}",
                DisplayName = $"N+1 stock-in product {index}",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsActive = true,
                IsSerialTracked = true
            }));
            seedContext.ProductUnits.AddRange(Enumerable.Range(0, lineCount).Select(index => new ProductUnit
            {
                ProductId = 500 + index,
                UnitId = 1,
                ConversionFactor = 1m,
                IsBaseUnit = true
            }));
            seedContext.SaveChanges();
        }

        var counter = new SelectCommandCounter();
        var service = new StockInService(() => DatabaseHelper.CreateContext(connection, counter));
        var stockIn = new StockIn
        {
            DocumentCode = $"SI-N1-{lineCount}",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase",
            CreatedAt = DateTime.UtcNow
        };
        var lines = Enumerable.Range(0, lineCount)
            .Select(index => new StockInLine
            {
                ProductId = 500 + index,
                UnitId = 1,
                Quantity = 1m,
                UnitPrice = 10m,
                ProductSerials =
                [
                    new ProductSerial { SerialNumber = $"N1-IN-SN-{index}" }
                ]
            })
            .ToList();

        service.SaveDraft(stockIn, lines, 1);
        ApproveForPosting(service, connection, stockIn.Id);
        counter.Reset();

        service.Post(stockIn.Id, 1);

        return counter.Count;
    }

    [Fact]
    public void Paged_results_are_stable_when_dates_and_created_times_match()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var timestamp = new DateTime(2026, 8, 13, 8, 30, 0, DateTimeKind.Utc);
        using (var db = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.StockIns.AddRange(Enumerable.Range(1, 205).Select(index => new StockIn
            {
                DocumentCode = $"PAGE-IN-{index:D3}",
                SupplierId = 1,
                WarehouseId = 1,
                PurposeCode = "Purchase",
                Status = DocumentStatus.Draft,
                ImportDate = timestamp,
                CreatedBy = 1,
                CreatedAt = timestamp
            }));
            db.SaveChanges();
        }
        var service = new StockInService(() => CreateContext(connection));

        var ids = service.GetStockInPaged("", "", null, null, null, "", 0, 100)
            .Concat(service.GetStockInPaged("", "", null, null, null, "", 100, 100))
            .Concat(service.GetStockInPaged("", "", null, null, null, "", 200, 100))
            .Select(item => item.Id)
            .ToList();

        Assert.Equal(205, ids.Count);
        Assert.Equal(205, ids.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(id => id), ids);
    }

    private static void ApproveForPosting(StockInService service, SqliteConnection connection, int stockInId)
    {
        using (var db = CreateContext(connection))
        {
            db.AppUsers.Find(1)!.RoleCode = "Quản lý";
            db.SaveChanges();
        }
        service.SubmitForApproval(stockInId, 1);
        service.Approve(stockInId, 1);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }
}
