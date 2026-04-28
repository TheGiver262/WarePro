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

    private static AppDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
