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
    public void WarrantyClaimTransitions_enforces_allowed_state_action_table()
    {
        var allowed = new Dictionary<string, HashSet<WarrantyClaimAction>>
        {
            ["Open"] = new()
            {
                WarrantyClaimAction.Resolve,
                WarrantyClaimAction.Send,
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.Reject
            },
            ["ManufacturerWait"] = new()
            {
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.Replace
            },
            ["Ready"] = new()
            {
                WarrantyClaimAction.Replace,
                WarrantyClaimAction.Close
            },
            ["Closed"] = new(),
            ["Rejected"] = new()
        };

        foreach (var (status, allowedActions) in allowed)
        {
            foreach (var action in Enum.GetValues<WarrantyClaimAction>())
            {
                if (allowedActions.Contains(action))
                {
                    WarrantyClaimTransitions.EnsureAllowed(status, action);
                }
                else
                {
                    Assert.Throws<InvalidOperationException>(() =>
                        WarrantyClaimTransitions.EnsureAllowed(status, action));
                }
            }
        }
    }

    [Fact]
    public void CoverageDates_reject_end_before_start()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WarrantyClaimService.EnsureValidCoverageDates(
                new DateTime(2026, 5, 2),
                new DateTime(2026, 5, 1)));
    }

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
    public void CreateClaim_wraps_database_constraint_errors()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            SeedOpenClaim(seedContext, "WARRANTY-DUP-CODE", "WC-DUP");
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        var exception = Assert.Throws<InvalidOperationException>(
            () => service.CreateClaim("WC-DUP", "WARRANTY-DUP-CODE", "Duplicate code", userId: 4));

        Assert.Contains("Không thể tạo phiếu bảo hành", exception.Message);
        Assert.IsType<DbUpdateException>(exception.InnerException);
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
            Assert.NotNull(serial);
            Assert.Equal("InWarrantyProcess", serial.CurrentStatus);

            var claim2 = assertContext.WarrantyClaims.First();
            var service2 = new WarrantyClaimService(() => assertContext);
            service2.DeleteClaim(claim2.Id);
        }

        using (var assertContext2 = CreateContext(connection))
        {
            var serial = assertContext2.ProductSerials.Find(serialId);
            Assert.NotNull(serial);
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

    [Fact]
    public void ReceiveFromManufacturerReplaced_updates_old_coverage_to_inactive_and_creates_new_coverage_with_remaining_days()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId;
        string newSerialNo = "NEW-WARRANTY-SERIAL-1";
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Add(new AppUser
            {
                Id = 4,
                Username = "warranty",
                FullName = "Warranty Staff",
                PasswordHash = "hash",
                RoleCode = "Nhân viên bảo hành",
                IsActive = true
            });
            seedContext.Products.Add(new Product { Id = 3000, ProductCode = "P3000", DisplayName = "P30", CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = true });
            
            var serial = new ProductSerial { SerialNumber = "OLD-WARRANTY-SERIAL-1", ProductId = 3000, CurrentStatus = "ReturnedToManufacturer" };
            seedContext.ProductSerials.Add(serial);
            seedContext.SaveChanges();
            
            var coverage = new WarrantyCoverage 
            { 
                ProductSerialId = serial.Id, 
                CustomerId = 1, 
                WarrantyStartDate = DateTime.Now.AddDays(-10), 
                WarrantyEndDate = DateTime.Now.AddDays(20), 
                CoverageStatus = "Active" 
            };
            seedContext.WarrantyCoverages.Add(coverage);
            seedContext.SaveChanges();

            var claim = new WarrantyClaim
            {
                ClaimCode = "WC-REPLACE-1",
                WarrantyCoverageId = coverage.Id,
                ProductSerialId = serial.Id,
                ReceivedDate = DateTime.Now,
                Status = "ManufacturerWait"
            };
            seedContext.WarrantyClaims.Add(claim);
            seedContext.SaveChanges();
            claimId = claim.Id;
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));
        service.ReceiveFromManufacturerReplaced(claimId, newSerialNo, "Replaced by manufacturer", userId: 4);

        using var assertContext = CreateContext(connection);
        var oldCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerial.SerialNumber == "OLD-WARRANTY-SERIAL-1");
        Assert.NotNull(oldCoverage);
        Assert.Equal("Inactive", oldCoverage.CoverageStatus);

        var newSerial = assertContext.ProductSerials.FirstOrDefault(s => s.SerialNumber == newSerialNo);
        Assert.NotNull(newSerial);

        var newCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == newSerial.Id);
        Assert.NotNull(newCoverage);
        Assert.Equal("Active", newCoverage.CoverageStatus);
        Assert.Equal(1, newCoverage.CustomerId);
        
        var remainingDays = (newCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
        Assert.True(remainingDays > 19 && remainingDays <= 20);
    }

    [Fact]
    public void ReplaceSerial_updates_old_coverage_to_inactive_and_creates_new_coverage_with_remaining_days()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        int claimId;
        string replacementSerialNo = "REPLACE-WARRANTY-SERIAL-2";
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.AppUsers.Add(new AppUser
            {
                Id = 4,
                Username = "warranty",
                FullName = "Warranty Staff",
                PasswordHash = "hash",
                RoleCode = "Nhân viên bảo hành",
                IsActive = true
            });
            seedContext.Products.Add(new Product { Id = 3001, ProductCode = "P3001", DisplayName = "P31", CategoryId = 1, BrandId = 1, DefaultUnitId = 1, DefaultPrice = 10m, IsSerialTracked = true });
            
            var serial = new ProductSerial { SerialNumber = "OLD-WARRANTY-SERIAL-2", ProductId = 3001, CurrentStatus = "InWarrantyProcess" };
            seedContext.ProductSerials.Add(serial);

            var replacementSerial = new ProductSerial { SerialNumber = replacementSerialNo, ProductId = 3001, CurrentStatus = "InStock", CurrentWarehouseId = 1 };
            seedContext.ProductSerials.Add(replacementSerial);
            seedContext.StockBalances.Add(new StockBalance
            {
                ProductId = 3001,
                WarehouseId = 1,
                OnHandQuantity = 1,
                AvailableQuantity = 1
            });
            seedContext.SaveChanges();
            
            var coverage = new WarrantyCoverage 
            { 
                ProductSerialId = serial.Id, 
                CustomerId = 1, 
                WarrantyStartDate = DateTime.Now.AddDays(-5), 
                WarrantyEndDate = DateTime.Now.AddDays(15), 
                CoverageStatus = "Active" 
            };
            seedContext.WarrantyCoverages.Add(coverage);
            seedContext.SaveChanges();

            var claim = new WarrantyClaim
            {
                ClaimCode = "WC-REPLACE-2",
                WarrantyCoverageId = coverage.Id,
                ProductSerialId = serial.Id,
                ReceivedDate = DateTime.Now,
                Status = "Ready"
            };
            seedContext.WarrantyClaims.Add(claim);
            seedContext.SaveChanges();
            claimId = claim.Id;
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));
        service.ReplaceSerial(claimId, replacementSerialNo, "Direct replacement", userId: 4);

        using var assertContext = CreateContext(connection);
        var oldCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerial.SerialNumber == "OLD-WARRANTY-SERIAL-2");
        Assert.NotNull(oldCoverage);
        Assert.Equal("Inactive", oldCoverage.CoverageStatus);

        var newSerial = assertContext.ProductSerials.FirstOrDefault(s => s.SerialNumber == replacementSerialNo);
        Assert.NotNull(newSerial);

        var newCoverage = assertContext.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == newSerial.Id);
        Assert.NotNull(newCoverage);
        Assert.Equal("Active", newCoverage.CoverageStatus);
        Assert.Equal(1, newCoverage.CustomerId);
        
        var remainingDays = (newCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
        Assert.True(remainingDays > 14 && remainingDays <= 15);
    }

    [Fact]
    public void ReplaceSerial_rejects_second_replacement_without_changing_inventory_coverage_or_links()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(connection, "Ready", twoReplacementSerials: true);
        var service = new WarrantyClaimService(() => CreateContext(connection));

        service.ReplaceSerial(claimId, "REPLACEMENT-1", "Approved replacement", userId: 4);

        ReplacementSnapshot before;
        using (var snapshotContext = CreateContext(connection))
        {
            before = ReadReplacementSnapshot(snapshotContext, claimId);
        }

        Assert.Throws<InvalidOperationException>(() =>
            service.ReplaceSerial(claimId, "REPLACEMENT-2", "Duplicate replacement", userId: 4));

        using var assertContext = CreateContext(connection);
        Assert.Equal(before, ReadReplacementSnapshot(assertContext, claimId));
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Rejected")]
    public void ReplaceSerial_rejects_terminal_claim_without_changing_inventory_coverage_or_links(string status)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(connection, status, twoReplacementSerials: false);
        var service = new WarrantyClaimService(() => CreateContext(connection));

        ReplacementSnapshot before;
        using (var snapshotContext = CreateContext(connection))
        {
            before = ReadReplacementSnapshot(snapshotContext, claimId);
        }

        Assert.Throws<InvalidOperationException>(() =>
            service.ReplaceSerial(claimId, "REPLACEMENT-1", "Forbidden replacement", userId: 4));

        using var assertContext = CreateContext(connection);
        Assert.Equal(before, ReadReplacementSnapshot(assertContext, claimId));
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Rejected")]
    public void UpdateClaim_rejects_terminal_claim_without_changes(string status)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(connection, status, twoReplacementSerials: false);
        WarrantyClaim claim;
        using (var arrangeContext = CreateContext(connection))
        {
            claim = arrangeContext.WarrantyClaims.AsNoTracking().Single(item => item.Id == claimId);
        }
        claim.ProcessingNote = "Forbidden edit";
        var service = new WarrantyClaimService(() => CreateContext(connection));

        Assert.Throws<InvalidOperationException>(() => service.UpdateClaim(claim));

        using var assertContext = CreateContext(connection);
        var unchanged = assertContext.WarrantyClaims.Single(item => item.Id == claimId);
        Assert.Null(unchanged.ProcessingNote);
        Assert.Equal(status, unchanged.Status);
    }

    private static int SeedReplacementClaim(
        SqliteConnection connection,
        string status,
        bool twoReplacementSerials)
    {
        using var context = CreateContext(connection);
        DatabaseHelper.SeedBasicData(context);
        context.AppUsers.Add(new AppUser
        {
            Id = 4,
            Username = "warranty-state",
            FullName = "Warranty Staff",
            PasswordHash = "hash",
            RoleCode = "Nhân viên bảo hành",
            IsActive = true
        });
        context.Products.Add(new Product
        {
            Id = 4000,
            ProductCode = "P4000",
            DisplayName = "Replacement product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsSerialTracked = true
        });

        var defective = new ProductSerial
        {
            SerialNumber = "DEFECTIVE-1",
            ProductId = 4000,
            CurrentStatus = "InWarrantyProcess"
        };
        context.ProductSerials.Add(defective);
        context.ProductSerials.Add(new ProductSerial
        {
            SerialNumber = "REPLACEMENT-1",
            ProductId = 4000,
            CurrentStatus = "InStock",
            CurrentWarehouseId = 1
        });
        if (twoReplacementSerials)
        {
            context.ProductSerials.Add(new ProductSerial
            {
                SerialNumber = "REPLACEMENT-2",
                ProductId = 4000,
                CurrentStatus = "InStock",
                CurrentWarehouseId = 1
            });
        }

        context.StockBalances.Add(new StockBalance
        {
            ProductId = 4000,
            WarehouseId = 1,
            OnHandQuantity = twoReplacementSerials ? 2 : 1,
            AvailableQuantity = twoReplacementSerials ? 2 : 1
        });
        context.SaveChanges();

        var coverage = new WarrantyCoverage
        {
            ProductSerialId = defective.Id,
            CustomerId = 1,
            WarrantyStartDate = DateTime.Now.AddDays(-5),
            WarrantyEndDate = DateTime.Now.AddDays(15),
            CoverageStatus = "Active"
        };
        context.WarrantyCoverages.Add(coverage);
        context.SaveChanges();

        var claim = new WarrantyClaim
        {
            ClaimCode = $"WC-{status}",
            WarrantyCoverageId = coverage.Id,
            ProductSerialId = defective.Id,
            ReceivedDate = DateTime.Now,
            Status = status
        };
        context.WarrantyClaims.Add(claim);
        context.SaveChanges();
        return claim.Id;
    }

    private static ReplacementSnapshot ReadReplacementSnapshot(AppDbContext context, int claimId)
    {
        var claim = context.WarrantyClaims.Single(item => item.Id == claimId);
        var serialStates = string.Join(",", context.ProductSerials
            .Where(serial => serial.ProductId == 4000)
            .OrderBy(serial => serial.SerialNumber)
            .Select(serial => $"{serial.SerialNumber}:{serial.CurrentStatus}"));
        var coverageStates = string.Join(",", context.WarrantyCoverages
            .OrderBy(coverage => coverage.Id)
            .Select(coverage => $"{coverage.ProductSerialId}:{coverage.CoverageStatus}"));
        var balance = context.StockBalances.Single(item => item.ProductId == 4000 && item.WarehouseId == 1);

        return new ReplacementSnapshot(
            balance.OnHandQuantity,
            balance.AvailableQuantity,
            serialStates,
            coverageStates,
            claim.ReplacementSerialId,
            claim.ReplacementStockOutId,
            context.StockOuts.Count(item => item.PurposeCode == "WarrantyReplacement"));
    }

    private sealed record ReplacementSnapshot(
        decimal OnHand,
        decimal Available,
        string SerialStates,
        string CoverageStates,
        int? ReplacementSerialId,
        int? ReplacementStockOutId,
        int ReplacementStockOutCount);
}

