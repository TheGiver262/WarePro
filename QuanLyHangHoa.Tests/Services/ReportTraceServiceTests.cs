using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class ReportTraceServiceTests
{
    [Fact]
    public void GetProductTimeline_returns_document_context_and_running_balance()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            SeedTraceData(seedContext);
        }

        var service = new ReportTraceService(() => DatabaseHelper.CreateContext(connection));

        var result = service.GetProductTimeline(
            productId: 100,
            fromDate: new DateTime(2026, 1, 1),
            toDate: new DateTime(2026, 1, 31));

        Assert.Equal(10m, result.StartQuantity);
        Assert.Equal(8m, result.EndQuantity);
        Assert.Equal(2, result.Items.Count);

        var stockIn = result.Items[0];
        Assert.Equal("SI-001", stockIn.DocumentCode);
        Assert.Equal("Nhap mua", stockIn.Purpose);
        Assert.Equal("General Supplier", stockIn.PartnerName);
        Assert.Equal("Main Warehouse", stockIn.WarehouseName);
        Assert.Equal(5m, stockIn.InQty);
        Assert.Equal(0m, stockIn.OutQty);
        Assert.Equal(15m, stockIn.BalanceQty);

        var stockOut = result.Items[1];
        Assert.Equal("SO-001", stockOut.DocumentCode);
        Assert.Equal("Xuat ban", stockOut.Purpose);
        Assert.Equal("General Customer", stockOut.PartnerName);
        Assert.Equal(0m, stockOut.InQty);
        Assert.Equal(7m, stockOut.OutQty);
        Assert.Equal(8m, stockOut.BalanceQty);
    }

    [Fact]
    public void SearchSerialTrace_returns_purchase_sale_warranty_and_current_state()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            SeedTraceData(seedContext);
        }

        var service = new ReportTraceService(() => DatabaseHelper.CreateContext(connection));

        var result = service.SearchSerialTrace(new SerialTraceFilter
        {
            SearchText = "SN-001",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 31)
        });

        var item = Assert.Single(result);
        Assert.Equal("SN-001", item.SerialNumber);
        Assert.Equal("Trace product", item.ProductName);
        Assert.Equal("Main Warehouse", item.CurrentWarehouseName);
        Assert.Equal("Sold", item.CurrentStatus);
        Assert.Equal("SI-001", item.ImportDocCode);
        Assert.Equal(new DateTime(2026, 1, 5, 8, 30, 0), item.ImportDate);
        Assert.Equal("General Supplier", item.SupplierName);
        Assert.Equal("SO-001", item.ExportDocCode);
        Assert.Equal(new DateTime(2026, 1, 10, 9, 45, 0), item.ExportDate);
        Assert.Equal("General Customer", item.CustomerName);
        Assert.Equal("SINV-001", item.SalesInvoiceCode);
        Assert.Equal(new DateTime(2026, 1, 10, 10, 0, 0), item.SalesInvoiceDate);
        Assert.Equal("Còn bảo hành", item.WarrantyStatus);
        Assert.Equal(new DateTime(2027, 1, 10), item.WarrantyEndDate);
    }

    [Fact]
    public void WarrantyCoverage_relationship_allows_history_per_serial()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = DatabaseHelper.CreateContext(connection);

        var foreignKey = context.Model.FindEntityType(typeof(WarrantyCoverage))!
            .GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(ProductSerial));

        Assert.False(foreignKey.IsUnique);
    }

    [Fact]
    public void SearchSerialTrace_prefers_active_coverage_over_newer_history()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = DatabaseHelper.CreateContext(connection))
        {
            SeedTraceData(seedContext);
            seedContext.WarrantyCoverages.Add(new WarrantyCoverage
            {
                Id = 51,
                ProductSerialId = 30,
                CustomerId = 1,
                SalesInvoiceId = 40,
                WarrantyStartDate = new DateTime(2028, 1, 10),
                WarrantyEndDate = new DateTime(2029, 1, 10),
                CoverageStatus = "Voided"
            });
            seedContext.SaveChanges();
        }

        var service = new ReportTraceService(() => DatabaseHelper.CreateContext(connection));

        var item = Assert.Single(service.SearchSerialTrace(new SerialTraceFilter
        {
            SearchText = "SN-001"
        }));

        Assert.Equal(new DateTime(2026, 1, 10), item.WarrantyStartDate);
        Assert.Equal(new DateTime(2027, 1, 10), item.WarrantyEndDate);
    }

    private static void SeedTraceData(AppDbContext context)
    {
        DatabaseHelper.SeedBasicData(context);

        context.Products.Add(new Product
        {
            Id = 100,
            ProductCode = "P100",
            DisplayName = "Trace product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 100m,
            CostPrice = 70m,
            IsSerialTracked = true,
            WarrantyPeriodMonths = 12,
            IsActive = true
        });

        context.StockIns.Add(new StockIn
        {
            Id = 10,
            DocumentCode = "SI-001",
            SupplierId = 1,
            WarehouseId = 1,
            PurposeCode = "Purchase",
            Status = "Posted",
            CreatedBy = 1,
            PostedBy = 1,
            CreatedAt = new DateTime(2026, 1, 5, 8, 0, 0),
            PostedAt = new DateTime(2026, 1, 5, 8, 30, 0),
            ImportDate = new DateTime(2026, 1, 5, 8, 30, 0)
        });

        context.StockInLines.Add(new StockInLine
        {
            Id = 11,
            StockInId = 10,
            ProductId = 100,
            UnitId = 1,
            Quantity = 5m,
            BaseQuantity = 5m,
            UnitPrice = 70m
        });

        context.StockOuts.Add(new StockOut
        {
            Id = 20,
            DocumentCode = "SO-001",
            CustomerId = 1,
            WarehouseId = 1,
            PurposeCode = "Sale",
            Status = "Posted",
            CreatedBy = 1,
            PostedBy = 1,
            CreatedAt = new DateTime(2026, 1, 10, 9, 0, 0),
            PostedAt = new DateTime(2026, 1, 10, 9, 45, 0),
            ExportDate = new DateTime(2026, 1, 10, 9, 45, 0)
        });

        context.StockOutLines.Add(new StockOutLine
        {
            Id = 21,
            StockOutId = 20,
            ProductId = 100,
            UnitId = 1,
            Quantity = 7m,
            BaseQuantity = 7m,
            UnitPrice = 150m
        });

        context.ProductSerials.Add(new ProductSerial
        {
            Id = 30,
            ProductId = 100,
            SerialNumber = "SN-001",
            CurrentStatus = "Sold",
            CurrentWarehouseId = 1,
            LastStockInLineId = 11,
            LastStockOutLineId = 21
        });

        context.SalesInvoices.Add(new SalesInvoice
        {
            Id = 40,
            InvoiceCode = "SINV-001",
            CustomerId = 1,
            StockOutId = 20,
            InvoiceDate = new DateTime(2026, 1, 10, 10, 0, 0),
            GrandTotal = 150m,
            CreatedBy = 1,
            CreatedAt = new DateTime(2026, 1, 10, 10, 0, 0)
        });

        context.WarrantyCoverages.Add(new WarrantyCoverage
        {
            Id = 50,
            ProductSerialId = 30,
            CustomerId = 1,
            SalesInvoiceId = 40,
            WarrantyStartDate = new DateTime(2026, 1, 10),
            WarrantyEndDate = new DateTime(2027, 1, 10),
            CoverageStatus = "Active"
        });

        context.StockLedgers.AddRange(
            new StockLedger
            {
                Id = 1,
                ProductId = 100,
                WarehouseId = 1,
                SourceDocumentType = "StockIn",
                SourceDocumentId = 9,
                MovementType = "In",
                Quantity = 10m,
                PostedAt = new DateTime(2025, 12, 20, 8, 0, 0),
                PostedBy = 1
            },
            new StockLedger
            {
                Id = 2,
                ProductId = 100,
                WarehouseId = 1,
                ProductSerialId = 30,
                SourceDocumentType = "StockIn",
                SourceDocumentId = 10,
                MovementType = "In",
                Quantity = 5m,
                PostedAt = new DateTime(2026, 1, 5, 8, 30, 0),
                PostedBy = 1
            },
            new StockLedger
            {
                Id = 3,
                ProductId = 100,
                WarehouseId = 1,
                ProductSerialId = 30,
                SourceDocumentType = "StockOut",
                SourceDocumentId = 20,
                MovementType = "Out",
                Quantity = 7m,
                PostedAt = new DateTime(2026, 1, 10, 9, 45, 0),
                PostedBy = 1
            });

        context.SaveChanges();
    }
}
