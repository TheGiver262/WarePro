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
    public void CreateClaim_allows_multiple_open_claims_for_same_serial()
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
                ProblemDescription = "Already open",
                ProcessedBy = 4
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        // Should allow creating another claim instead of throwing exception
        var claimId2 = service.CreateClaim("WC-0002", "WARRANTY-002", "Battery issue", userId: 4);

        using var assertContext = CreateContext(connection);
        var claims = assertContext.WarrantyClaims.ToList();
        Assert.Equal(2, claims.Count);
        Assert.Contains(claims, c => c.ClaimCode == "WC-OPEN");
        Assert.Contains(claims, c => c.ClaimCode == "WC-0002");
    }

    [Fact]
    public void DeleteClaim_rejects_when_has_related_stockout_or_stockin()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId;
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 1002, ProductCode = "P1002", DisplayName = "P2", CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = true });
            var serial = new ProductSerial { SerialNumber = "SERIAL-DELETE-TEST", ProductId = 1002, CurrentStatus = "InWarrantyProcess" };
            seedContext.ProductSerials.Add(serial);
            seedContext.SaveChanges();
            
            var coverage = new WarrantyCoverage { ProductSerialId = serial.Id, CustomerId = 1, WarrantyStartDate = new DateTime(2026, 1, 1), WarrantyEndDate = new DateTime(2027, 1, 1), CoverageStatus = "Active" };
            seedContext.WarrantyCoverages.Add(coverage);
            
            var claim = new WarrantyClaim
            {
                ClaimCode = "WC-DEL",
                WarrantyCoverageId = coverage.Id,
                ProductSerialId = serial.Id,
                ReceivedDate = DateTime.Now,
                Status = "Open",
                ReplacementStockOutId = 999
            };
            seedContext.WarrantyClaims.Add(claim);
            seedContext.SaveChanges();
            claimId = claim.Id;
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));
        var ex = Assert.Throws<InvalidOperationException>(() => service.DeleteClaim(claimId));
        Assert.Contains("Không thể xóa phiếu bảo hành", ex.Message);
    }

    [Fact]
    public void DeleteClaim_restores_serial_status_only_when_no_other_open_claims()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId1;
        int serialId;
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product { Id = 1003, ProductCode = "P1003", DisplayName = "P3", CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = true });
            var serial = new ProductSerial { SerialNumber = "SERIAL-MULTI-TEST", ProductId = 1003, CurrentStatus = "InWarrantyProcess" };
            seedContext.ProductSerials.Add(serial);
            seedContext.SaveChanges();
            serialId = serial.Id;

            var coverage = new WarrantyCoverage { ProductSerialId = serial.Id, CustomerId = 1, WarrantyStartDate = new DateTime(2026, 1, 1), WarrantyEndDate = new DateTime(2027, 1, 1), CoverageStatus = "Active" };
            seedContext.WarrantyCoverages.Add(coverage);

            var claim1 = new WarrantyClaim { ClaimCode = "WC-1", WarrantyCoverageId = coverage.Id, ProductSerialId = serial.Id, ReceivedDate = DateTime.Now, Status = "Open" };
            var claim2 = new WarrantyClaim { ClaimCode = "WC-2", WarrantyCoverageId = coverage.Id, ProductSerialId = serial.Id, ReceivedDate = DateTime.Now, Status = "Open" };
            seedContext.WarrantyClaims.AddRange(claim1, claim2);
            seedContext.SaveChanges();
            claimId1 = claim1.Id;
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        service.DeleteClaim(claimId1);

        using (var assertContext = CreateContext(connection))
        {
            var serial = assertContext.ProductSerials.Find(serialId);
            Assert.Equal("InWarrantyProcess", serial.CurrentStatus);

            var claim2 = assertContext.WarrantyClaims.First();
            var service2 = new WarrantyClaimService(() => assertContext);
            service2.DeleteClaim(claim2.Id);
        }

        using (var assertContext2 = CreateContext(connection))
        {
            var serial = assertContext2.ProductSerials.Find(serialId);
            Assert.Equal("Sold", serial.CurrentStatus);
        }
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
