using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

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
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 1000,
                Name = "Warranty product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = true
            });
            var serial = new ProductSerial
            {
                SerialNumber = "WARRANTY-001",
                ProductId = 1000,
                Status = SerialStatus.Sold.ToString()
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

        var service = new WarrantyClaimService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 28, 10, 0, 0));

        var claimId = service.CreateClaim("WC-0001", "WARRANTY-001", "Screen flicker", receivedBy: 4);

        using var assertContext = CreateContext(connection);
        var claim = Assert.Single(assertContext.WarrantyClaims);
        Assert.Equal(claimId, claim.Id);
        Assert.Equal("WC-0001", claim.ClaimCode);
        Assert.Equal("Checking", claim.ClaimStatus);
        Assert.Equal("Screen flicker", claim.ProblemDescription);
        Assert.Equal(serialId, claim.ProductSerialId);
        Assert.Equal(4, claim.ProcessedBy);
        Assert.Equal(SerialStatus.InWarrantyProcess.ToString(), assertContext.ProductSerials.Single().Status);
    }

    [Fact]
    public void CreateClaim_rejects_when_serial_already_has_open_claim()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            seedContext.Products.Add(new Product
            {
                Id = 1001,
                Name = "Warranty product",
                CategoryId = 1,
                BrandId = 1,
                UnitId = 1,
                Quantity = 99,
                UnitPrice = 10m,
                IsSerialManaged = true
            });
            var serial = new ProductSerial
            {
                SerialNumber = "WARRANTY-002",
                ProductId = 1001,
                Status = SerialStatus.Sold.ToString()
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
                ClaimStatus = "Checking",
                ProblemDescription = "Already open"
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 28, 10, 0, 0));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.CreateClaim("WC-0002", "WARRANTY-002", "Battery issue", receivedBy: 4));

        Assert.Equal("Serial WARRANTY-002 already has an open warranty claim.", ex.Message);
    }

    [Fact]
    public void CompleteRepair_closes_claim_and_marks_original_serial_sold()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            claimId = SeedOpenClaim(seedContext, "WARRANTY-REPAIR-001", "WC-REPAIR");
        }

        var service = new WarrantyClaimService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 11, 0, 0));

        service.CompleteRepair(claimId, "Fixed mainboard", processedBy: 5);

        using var assertContext = CreateContext(connection);
        var claim = assertContext.WarrantyClaims.Single();
        Assert.Equal("ReturnedToCustomer", claim.ClaimStatus);
        Assert.Equal("Fixed mainboard", claim.TechnicalConclusion);
        Assert.Equal(new DateTime(2026, 4, 29, 11, 0, 0), claim.ClosedDate);
        Assert.Equal(5, claim.ProcessedBy);
        Assert.Equal(SerialStatus.Sold.ToString(), assertContext.ProductSerials.Single().Status);
    }

    [Fact]
    public void RejectClaim_requires_reason_and_returns_serial_to_customer()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            claimId = SeedOpenClaim(seedContext, "WARRANTY-REJECT-001", "WC-REJECT");
        }

        var service = new WarrantyClaimService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 12, 0, 0));

        service.RejectClaim(claimId, "Vo roi khong du dieu kien", processedBy: 6);

        using var assertContext = CreateContext(connection);
        var claim = assertContext.WarrantyClaims.Single();
        Assert.Equal("ReturnedToCustomer", claim.ClaimStatus);
        Assert.Equal("Vo roi khong du dieu kien", claim.RejectionReason);
        Assert.Equal(new DateTime(2026, 4, 29, 12, 0, 0), claim.ClosedDate);
        Assert.Equal(6, claim.ProcessedBy);
        Assert.Equal(SerialStatus.Sold.ToString(), assertContext.ProductSerials.Single().Status);
    }

    [Fact]
    public void ReplaceSerial_closes_claim_and_creates_replacement_coverage_for_remaining_period()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId;
        using (var seedContext = CreateContext(connection))
        {
            seedContext.Database.EnsureCreated();
            claimId = SeedOpenClaim(seedContext, "WARRANTY-OLD-001", "WC-REPLACE");
            seedContext.ProductSerials.Add(new ProductSerial
            {
                SerialNumber = "WARRANTY-NEW-001",
                ProductId = 1000,
                Status = SerialStatus.InStock.ToString(),
                CurrentWarehouseId = 1
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(
            () => CreateContext(connection),
            () => new DateTime(2026, 4, 29, 13, 0, 0));

        service.ReplaceSerial(claimId, "WARRANTY-NEW-001", "Approved replacement", processedBy: 7);

        using var assertContext = CreateContext(connection);
        var claim = assertContext.WarrantyClaims.Single();
        var oldSerial = assertContext.ProductSerials.Single(serial => serial.SerialNumber == "WARRANTY-OLD-001");
        var newSerial = assertContext.ProductSerials.Single(serial => serial.SerialNumber == "WARRANTY-NEW-001");
        var newCoverage = assertContext.WarrantyCoverages.Single(coverage => coverage.ProductSerialId == newSerial.Id);

        Assert.Equal("Replaced", claim.ClaimStatus);
        Assert.Equal(newSerial.Id, claim.ReplacementSerialId);
        Assert.Equal("Approved replacement", claim.TechnicalConclusion);
        Assert.Equal(new DateTime(2026, 4, 29, 13, 0, 0), claim.ClosedDate);
        Assert.Equal(SerialStatus.Replaced.ToString(), oldSerial.Status);
        Assert.Equal(SerialStatus.Sold.ToString(), newSerial.Status);
        Assert.Null(newSerial.CurrentWarehouseId);
        Assert.Equal(new DateTime(2026, 4, 29, 13, 0, 0), newCoverage.WarrantyStartDate);
        Assert.Equal(new DateTime(2027, 1, 1), newCoverage.WarrantyEndDate);
        Assert.Equal("Active", newCoverage.CoverageStatus);
    }

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static int SeedOpenClaim(AppDbContext context, string serialNumber, string claimCode)
    {
        context.Products.Add(new Product
        {
            Id = 1000,
            Name = "Warranty product",
            CategoryId = 1,
            BrandId = 1,
            UnitId = 1,
            Quantity = 99,
            UnitPrice = 10m,
            IsSerialManaged = true
        });
        var serial = new ProductSerial
        {
            SerialNumber = serialNumber,
            ProductId = 1000,
            Status = SerialStatus.InWarrantyProcess.ToString()
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
            ClaimStatus = "Checking",
            ProblemDescription = "Open issue"
        };
        context.WarrantyClaims.Add(claim);
        context.SaveChanges();
        return claim.Id;
    }
}
