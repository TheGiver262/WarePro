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

public class StockOutServiceTests
{
    [Fact]
    public void List_stats_and_filters_preserve_all_lifecycle_states()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            var now = DateTime.UtcNow;
            db.StockOuts.AddRange(
                CreateLifecycleDocument("LIFE-OUT-D", DocumentStatus.Draft, now),
                CreateLifecycleDocument("LIFE-OUT-P", DocumentStatus.PendingApproval, now),
                CreateLifecycleDocument("LIFE-OUT-A", DocumentStatus.Approved, now),
                CreateLifecycleDocument("LIFE-OUT-X", DocumentStatus.Posted, now));
            db.SaveChanges();
        }
        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));

        var stats = service.GetStockOutStats("LIFE-OUT", "", null, null, null, "Tất cả");

        Assert.Equal(4, stats.TotalCount);
        Assert.Equal(1, stats.DraftCount);
        Assert.Equal(1, stats.PendingApprovalCount);
        Assert.Equal(1, stats.ApprovedCount);
        Assert.Equal(1, stats.PostedCount);
        Assert.Equal(DocumentStatus.Draft, Assert.Single(service.GetStockOutPaged("LIFE-OUT", "", null, null, null, "Phiếu nháp", 0, 10)).Status);
        Assert.Equal(DocumentStatus.PendingApproval, Assert.Single(service.GetStockOutPaged("LIFE-OUT", "", null, null, null, "Chờ duyệt", 0, 10)).Status);
        Assert.Equal(DocumentStatus.Approved, Assert.Single(service.GetStockOutPaged("LIFE-OUT", "", null, null, null, "Đã duyệt", 0, 10)).Status);
        Assert.Equal(DocumentStatus.Posted, Assert.Single(service.GetStockOutPaged("LIFE-OUT", "", null, null, null, "Đã ghi sổ", 0, 10)).Status);
    }

    private static StockOut CreateLifecycleDocument(string code, string status, DateTime now) => new()
    {
        DocumentCode = code,
        CustomerId = 1,
        WarehouseId = 1,
        PurposeCode = "Sale",
        Status = status,
        ExportDate = now,
        CreatedBy = 1,
        CreatedAt = now
    };

    [Fact]
    public async Task SaveDraft_allocates_document_code_when_new_document_code_is_blank()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
        }
        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));
        var document = new StockOut
        {
            DocumentCode = string.Empty,
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale"
        };

        await service.SaveDraftAsync(document, [], 1, Guid.NewGuid());

        Assert.Matches("^OUT-[0-9]{8}-[0-9]{6}$", document.DocumentCode);
    }

    [Fact]
    public void Create_posts_to_database_and_updates_inventory()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 300, ProductCode = "P300",
                DisplayName = "Service stock-out product",
                CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = false });
            seedContext.ProductUnits.Add(new ProductUnit { ProductId = 300, UnitId = 1, ConversionFactor = 1m, IsBaseUnit = true });
            seedContext.StockBalances.Add(new StockBalance { ProductId = 300, WarehouseId = 1, OnHandQuantity = 5, AvailableQuantity = 5 });
            seedContext.SaveChanges();
        }

        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));
        var stockOut = new StockOut
        {
            DocumentCode = "SO-001",
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale",
            Status = "Draft", // Service will change it to Posted
            CreatedAt = new DateTime(2026, 4, 27, 12, 0, 0)
        };
        var lines = new List<StockOutLine>
        {
            new StockOutLine
            {
                ProductId = 300,
                UnitId = 1,
                Quantity = 2,
                UnitPrice = 15m
            }
        };

        GrantApprovalPermission(connection);
        service.Create(stockOut, lines, 1);

        var stats = service.GetStockOutStats(string.Empty, string.Empty, null, null, null, string.Empty);
        Assert.Equal(1, stats.TotalCount);
        Assert.Equal(0, stats.DraftCount);
        Assert.Equal(1, stats.PostedCount);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var savedStockOut = assertContext.StockOuts.Include(s => s.Lines).Single();
        Assert.Equal("SO-001", savedStockOut.DocumentCode);
        Assert.Equal(1, savedStockOut.CreatedBy);

        var balance = assertContext.StockBalances.Single(b => b.ProductId == 300);
        Assert.Equal(3, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 300);
        Assert.Equal("Out", ledger.MovementType);
        Assert.Equal(2, ledger.Quantity);
    }

    [Fact]
    public void SaveDraft_allows_same_serials_but_post_validates_and_throws_correct_errors()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product 
            { 
                Id = 301, 
                ProductCode = "P301",
                DisplayName = "Serial Product",
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 20m, 
                IsSerialTracked = true 
            });
            seedContext.StockBalances.Add(new StockBalance 
            { 
                ProductId = 301, 
                WarehouseId = 1, 
                OnHandQuantity = 5, 
                AvailableQuantity = 5 
            });
            seedContext.ProductSerials.AddRange(
                new ProductSerial { SerialNumber = "SN-101", ProductId = 301, CurrentWarehouseId = 1, CurrentStatus = "InStock" },
                new ProductSerial { SerialNumber = "SN-102", ProductId = 301, CurrentWarehouseId = 1, CurrentStatus = "InStock" }
            );
            seedContext.ProductUnits.Add(new ProductUnit { ProductId = 301, UnitId = 1, ConversionFactor = 1m, IsBaseUnit = true });
            seedContext.SaveChanges();
        }

        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));

        // Draft A
        var stockOutA = new StockOut { DocumentCode = "SO-A", CustomerId = 1, WarehouseId = 1, PurposeCode = "Sale" };
        var linesA = new List<StockOutLine>
        {
            new StockOutLine
            {
                ProductId = 301,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 20m,
                ProductSerials = new List<ProductSerial> { new ProductSerial { SerialNumber = "SN-101" } }
            }
        };

        // Draft B
        var stockOutB = new StockOut { DocumentCode = "SO-B", CustomerId = 1, WarehouseId = 1, PurposeCode = "Sale" };
        var linesB = new List<StockOutLine>
        {
            new StockOutLine
            {
                ProductId = 301,
                UnitId = 1,
                Quantity = 1,
                UnitPrice = 20m,
                ProductSerials = new List<ProductSerial> { new ProductSerial { SerialNumber = "SN-101" } }
            }
        };

        // 1. Both drafts should be saved successfully with the same serials
        service.SaveDraft(stockOutA, linesA, 1);
        service.SaveDraft(stockOutB, linesB, 1);

        using (var db = DatabaseHelper.CreateContext(connection))
        {
            Assert.Contains(db.StockOutLines, l => l.DraftSerials == "SN-101");
            Assert.Equal(2, db.StockOutLines.Count(l => l.DraftSerials == "SN-101"));
        }

        // 2. Post Draft A -> should succeed and set SN-101 to Sold (not in warehouse)
        ApproveForPosting(service, connection, stockOutA.Id);
        service.Post(stockOutA.Id, 1);

        using (var db = DatabaseHelper.CreateContext(connection))
        {
            var postedA = db.StockOuts.Find(stockOutA.Id);
            Assert.NotNull(postedA);
            Assert.Equal(DocumentStatus.Posted, postedA.Status);
            var serial = db.ProductSerials.Single(ps => ps.SerialNumber == "SN-101");
            Assert.Equal("Sold", serial.CurrentStatus);
            Assert.Null(serial.CurrentWarehouseId);
        }

        // 3. Post Draft B -> should fail because "SN-101" is no longer InStock / in this warehouse
        ApproveForPosting(service, connection, stockOutB.Id);
        var ex = Assert.Throws<Exception>(() => service.Post(stockOutB.Id, 1));
        Assert.Equal("Các số serial sau đã được xuất kho ở phiếu khác hoặc không còn tồn kho trong kho này: [SN-101]. Vui lòng sửa lại phiếu nháp trước khi duyệt.", ex.Message);

        // 4. Test document-level duplicate serials: Draft C with duplicate serials in the same document
        var stockOutC = new StockOut { DocumentCode = "SO-C", CustomerId = 1, WarehouseId = 1, PurposeCode = "Sale" };
        var linesC = new List<StockOutLine>
        {
            new StockOutLine
            {
                ProductId = 301,
                UnitId = 1,
                Quantity = 2,
                UnitPrice = 20m,
                ProductSerials = new List<ProductSerial> 
                { 
                    new ProductSerial { SerialNumber = "SN-102" },
                    new ProductSerial { SerialNumber = "SN-102" }
                }
            }
        };
        service.SaveDraft(stockOutC, linesC, 1);

        var exDup = Assert.Throws<InventoryDomainException>(() =>
            ApproveForPosting(service, connection, stockOutC.Id));
        Assert.Equal("Các số serial sau bị trùng lặp trong phiếu: [SN-102]. Vui lòng kiểm tra lại trước khi duyệt.", exDup.Message);
    }

    [Fact]
    public void Post_updates_inventory_using_BaseQuantity_when_unit_conversion_is_applied()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Units.Add(new Unit { Id = 2, UnitCode = "BOX", DisplayName = "Box", IsActive = true });
            seedContext.Products.Add(new Product 
            { 
                Id = 400, 
                ProductCode = "P400",
                DisplayName = "Converted Unit Product Out",
                CategoryId = 1, 
                BrandId = 1, 
                DefaultUnitId = 1, 
                DefaultPrice = 10m, 
                IsSerialTracked = false 
            });
            seedContext.ProductUnits.Add(new ProductUnit
            {
                ProductId = 400,
                UnitId = 2,
                ConversionFactor = 12m,
                IsBaseUnit = false,
                IsPurchaseUnit = true,
                IsSalesUnit = true
            });
            // Tồn ban đầu: 50 đơn vị cơ sở
            seedContext.StockBalances.Add(new StockBalance { ProductId = 400, WarehouseId = 1, OnHandQuantity = 50, AvailableQuantity = 50 });
            seedContext.SaveChanges();
        }

        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));
        var stockOut = new StockOut
        {
            DocumentCode = "SO-CONV",
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale",
            CreatedAt = DateTime.Now
        };
        var lines = new List<StockOutLine>
        {
            new StockOutLine
            {
                ProductId = 400,
                UnitId = 2,
                Quantity = 2,
                UnitPrice = 15m
            }
        };

        service.SaveDraft(stockOut, lines, 1);
        ApproveForPosting(service, connection, stockOut.Id);
        service.Post(stockOut.Id, 1);

        using var assertContext = DatabaseHelper.CreateContext(connection);
        var balance = assertContext.StockBalances.Single(b => b.ProductId == 400);
        // Xác minh tồn kho giảm theo BaseQuantity (50 - 24 = 26) chứ không phải Quantity giao dịch (50 - 2 = 48)
        Assert.Equal(26, balance.OnHandQuantity);

        var ledger = assertContext.StockLedgers.Single(l => l.ProductId == 400);
        Assert.Equal("Out", ledger.MovementType);
        Assert.Equal(24, ledger.Quantity);
    }

    [Fact]
    public async Task SubmitForApproval_rejects_invalid_serial_draft_and_keeps_status_draft()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product
            {
                Id = 450,
                ProductCode = "P450-OUT",
                DisplayName = "Submit validation product out",
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

        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));
        var document = new StockOut
        {
            DocumentCode = "SO-INVALID-SUBMIT",
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale"
        };
        await service.SaveDraftAsync(document,
        [
            new StockOutLine
            {
                ProductId = 450,
                UnitId = 1,
                Quantity = 2m,
                ProductSerials = [new ProductSerial { SerialNumber = "ONLY-ONE" }]
            }
        ], 1, Guid.NewGuid());

        await Assert.ThrowsAsync<InventoryDomainException>(() => service.SubmitForApprovalAsync(
            document.Id, document.RowVersion, 1, Guid.NewGuid()));

        using var assertContext = DatabaseHelper.CreateContext(connection);
        Assert.Equal(DocumentStatus.Draft, assertContext.StockOuts.Single(item => item.Id == document.Id).Status);
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
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            foreach (var index in Enumerable.Range(0, lineCount))
            {
                var productId = 600 + index;
                seedContext.Products.Add(new Product
                {
                    Id = productId,
                    ProductCode = $"N1-OUT-{index}",
                    DisplayName = $"N+1 stock-out product {index}",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsActive = true,
                    IsSerialTracked = true
                });
                seedContext.ProductUnits.Add(new ProductUnit
                {
                    ProductId = productId,
                    UnitId = 1,
                    ConversionFactor = 1m,
                    IsBaseUnit = true
                });
                seedContext.StockBalances.Add(new StockBalance
                {
                    ProductId = productId,
                    WarehouseId = 1,
                    OnHandQuantity = 10m,
                    AvailableQuantity = 10m
                });
                seedContext.ProductSerials.Add(new ProductSerial
                {
                    SerialNumber = $"N1-OUT-SN-{index}",
                    ProductId = productId,
                    CurrentWarehouseId = 1,
                    CurrentStatus = "InStock",
                    LastStockInLineId = 1
                });
            }
            seedContext.SaveChanges();
        }

        var counter = new SelectCommandCounter();
        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection, counter));
        var stockOut = new StockOut
        {
            DocumentCode = $"SO-N1-{lineCount}",
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale",
            CreatedAt = DateTime.UtcNow
        };
        var lines = Enumerable.Range(0, lineCount)
            .Select(index => new StockOutLine
            {
                ProductId = 600 + index,
                UnitId = 1,
                Quantity = 1m,
                UnitPrice = 10m,
                ProductSerials =
                [
                    new ProductSerial { SerialNumber = $"N1-OUT-SN-{index}" }
                ]
            })
            .ToList();

        service.SaveDraft(stockOut, lines, 1);
        ApproveForPosting(service, connection, stockOut.Id);
        counter.Reset();

        service.Post(stockOut.Id, 1);

        return counter.Count;
    }

    [Fact]
    public void Paged_results_are_stable_when_dates_and_created_times_match()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var timestamp = new DateTime(2026, 8, 13, 8, 30, 0, DateTimeKind.Utc);
        using (var db = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(db);
            db.StockOuts.AddRange(Enumerable.Range(1, 205).Select(index => new StockOut
            {
                DocumentCode = $"PAGE-OUT-{index:D3}",
                CustomerId = 1,
                WarehouseId = 1,
                PurposeCode = "Sale",
                Status = DocumentStatus.Draft,
                ExportDate = timestamp,
                CreatedBy = 1,
                CreatedAt = timestamp
            }));
            db.SaveChanges();
        }
        var service = new StockOutService(() => DatabaseHelper.CreateContext(connection));

        var ids = service.GetStockOutPaged("", "", null, null, null, "", 0, 100)
            .Concat(service.GetStockOutPaged("", "", null, null, null, "", 100, 100))
            .Concat(service.GetStockOutPaged("", "", null, null, null, "", 200, 100))
            .Select(item => item.Id)
            .ToList();

        Assert.Equal(205, ids.Count);
        Assert.Equal(205, ids.Distinct().Count());
        Assert.Equal(ids.OrderByDescending(id => id), ids);
    }

    private static void GrantApprovalPermission(SqliteConnection connection)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        db.AppUsers.Find(1)!.RoleCode = "Quản lý";
        db.SaveChanges();
    }

    private static void ApproveForPosting(StockOutService service, SqliteConnection connection, int stockOutId)
    {
        GrantApprovalPermission(connection);
        service.SubmitForApproval(stockOutId, 1);
        service.Approve(stockOutId, 1);
    }
}
