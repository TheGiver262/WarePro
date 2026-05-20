using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;
using System;
using System.Linq;

namespace QuanLyHangHoa.Tests.Services;

public class WarrantyClaimServiceTests
{
    [Fact]
    public void CreateClaim_for_active_coverage_creates_checking_claim_and_marks_serial_in_warranty()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int serialId;
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 1000, ProductCode = "P1000",
                DisplayName = "Warranty product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = true
                 });
            var serial = new ProductSerial { SerialNumber = "WARRANTY-001",
                ProductId = 1000,
                CurrentStatus = SerialStatus.Sold.ToString()
                 };
            seedContext.ProductSerials.Add(serial);
            seedContext.SaveChanges();
            serialId = serial.Id;
            seedContext.WarrantyCoverages.Add(new WarrantyCoverage
            {
                ProductSerialId = serialId,
                CustomerId = 1,
                WarrantyStartDate = new DateTime(2026, 1, 1),
                WarrantyEndDate = new DateTime(2027, 1, 1),
                CoverageStatus = "Active"
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        var claimId = service.CreateClaim("WC-0001", "WARRANTY-001", "Screen flicker", userId: 4);

        using var assertContext = CreateContext(connection);
        var claim = Assert.Single(assertContext.WarrantyClaims);
        Assert.Equal(claimId, claim.Id);
        Assert.Equal("WC-0001", claim.ClaimCode);
        Assert.Equal("Open", claim.Status);
        Assert.Equal("Screen flicker", claim.ProblemDescription);
        Assert.Equal(serialId, claim.ProductSerialId);
        Assert.Equal(4, claim.ProcessedBy);
    }

    [Fact]
    public void CreateClaim_rejects_when_serial_already_has_open_claim()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 1001, ProductCode = "P1001",
                DisplayName = "Warranty product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = true
                 });
            var serial = new ProductSerial { SerialNumber = "WARRANTY-002",
                ProductId = 1001,
                CurrentStatus = SerialStatus.Sold.ToString()
                 };
            seedContext.ProductSerials.Add(serial);
            seedContext.SaveChanges();
            var coverage = new WarrantyCoverage
            {
                ProductSerialId = serial.Id,
                CustomerId = 1,
                WarrantyStartDate = new DateTime(2026, 1, 1),
                WarrantyEndDate = new DateTime(2027, 1, 1),
                CoverageStatus = "Active"
            };
            seedContext.WarrantyCoverages.Add(coverage);
            seedContext.SaveChanges();
            seedContext.WarrantyClaims.Add(new WarrantyClaim
            {
                ClaimCode = "WC-OPEN",
                WarrantyCoverageId = coverage.Id,
                ProductSerialId = serial.Id,
                ReceivedDate = new DateTime(2026, 4, 20),
                Status = "Open",
                ProblemDescription = "Already open"
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.CreateClaim("WC-0002", "WARRANTY-002", "Battery issue", userId: 4));

        Assert.Equal("Serial WARRANTY-002 already has an open warranty claim.", ex.Message);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        return DatabaseHelper.CreateContext(connection);
    }

    private static int SeedOpenClaim(AppDbContext context, string serialNumber, string claimCode)
    {
        context.Products.Add(new Product { Id = 2000, ProductCode = "P2000-" + Guid.NewGuid().ToString().Substring(0,4),
            DisplayName = "Warranty product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsSerialTracked = true
             });
        var serial = new ProductSerial { SerialNumber = serialNumber,
            ProductId = 2000,
            CurrentStatus = SerialStatus.InWarrantyProcess.ToString()
             };
        context.ProductSerials.Add(serial);
        context.SaveChanges();
        var coverage = new WarrantyCoverage
        {
            ProductSerialId = serial.Id,
            CustomerId = 1,
            WarrantyStartDate = new DateTime(2026, 1, 1),
            WarrantyEndDate = new DateTime(2027, 1, 1),
            CoverageStatus = "Active"
        };
        context.WarrantyCoverages.Add(coverage);
        context.SaveChanges();
        var claim = new WarrantyClaim
        {
            ClaimCode = claimCode,
            WarrantyCoverageId = coverage.Id,
            ProductSerialId = serial.Id,
            ReceivedDate = new DateTime(2026, 4, 28),
            Status = "Open",
            ProblemDescription = "Open issue"
        };
        context.WarrantyClaims.Add(claim);
        context.SaveChanges();
        return claim.Id;
    }
}
