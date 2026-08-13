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
                WarrantyClaimAction.CompleteShopRepair,
                WarrantyClaimAction.Reject
            },
            ["ManufacturerWait"] = new()
            {
                WarrantyClaimAction.Repair,
                WarrantyClaimAction.Replace,
                WarrantyClaimAction.ReceiveManufacturerRepair,
                WarrantyClaimAction.ReceiveManufacturerReplacement
            },
            ["Ready"] = new()
            {
                WarrantyClaimAction.Replace,
                WarrantyClaimAction.ReplaceFromStock,
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
    public void WarrantyClaimTransitions_requires_replacement_resolution_for_stock_replacement()
    {
        var repairClaim = new WarrantyClaim
        {
            Status = "Ready",
            ResolutionType = "Repair"
        };
        var replacementClaim = new WarrantyClaim
        {
            Status = "Ready",
            ResolutionType = "Replace"
        };

        Assert.False(WarrantyClaimTransitions.IsAllowed(
            repairClaim,
            WarrantyClaimAction.ReplaceFromStock));
        WarrantyClaimTransitions.EnsureAllowed(
            replacementClaim,
            WarrantyClaimAction.ReplaceFromStock);
    }

    [Fact]
    public void CoverageDates_reject_end_before_start()
    {
        Assert.Throws<InventoryDomainException>(() =>
            WarrantyClaimService.EnsureValidCoverageDates(
                new DateTime(2026, 5, 2),
                new DateTime(2026, 5, 1)));
    }

    [Fact]
    public void Database_rejects_claim_whose_coverage_belongs_to_another_serial()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var context = CreateContext(connection);
        DatabaseHelper.SeedBasicData(context);
        context.Products.Add(new Product
        {
            Id = 1010,
            ProductCode = "P1010",
            DisplayName = "Coverage consistency product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsSerialTracked = true
        });
        var coveredSerial = new ProductSerial
        {
            ProductId = 1010,
            SerialNumber = "COVERED-SERIAL",
            CurrentStatus = SerialStatus.Sold.ToString()
        };
        var otherSerial = new ProductSerial
        {
            ProductId = 1010,
            SerialNumber = "OTHER-SERIAL",
            CurrentStatus = SerialStatus.Sold.ToString()
        };
        context.ProductSerials.AddRange(coveredSerial, otherSerial);
        context.SaveChanges();
        var coverage = new WarrantyCoverage
        {
            ProductSerialId = coveredSerial.Id,
            CustomerId = 1,
            WarrantyStartDate = DateTime.Today,
            WarrantyEndDate = DateTime.Today.AddYears(1),
            CoverageStatus = "Active"
        };
        context.WarrantyCoverages.Add(coverage);
        context.SaveChanges();
        context.ChangeTracker.Clear();
        context.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;");
        context.WarrantyClaims.Add(new WarrantyClaim
        {
            ClaimCode = "CLAIM-MISMATCH",
            WarrantyCoverageId = coverage.Id,
            ProductSerialId = otherSerial.Id,
            ReceivedDate = DateTime.Today,
            Status = "Open",
            ProcessedBy = 1
        });

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }

    [Fact]
    public void CreateClaim_accepts_coverage_starting_later_today()
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
                WarrantyStartDate = DateTime.Today.AddHours(23),
                WarrantyEndDate = DateTime.Today.AddDays(30),
                CoverageStatus = "Active"
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        Assert.NotNull(service.GetCoverageBySerial("WARRANTY-001"));
        var claimId = service.CreateClaim("WC-0001", "WARRANTY-001", "Screen flicker", userId: 1);

        using var assertContext = CreateContext(connection);
        var claim = Assert.Single(assertContext.WarrantyClaims);
        Assert.Equal(claimId, claim.Id);
        Assert.Equal("WC-0001", claim.ClaimCode);
        Assert.Equal("Open", claim.Status);
        Assert.Equal("Screen flicker", claim.ProblemDescription);
        Assert.Equal(serialId, claim.ProductSerialId);
        Assert.Equal(1, claim.ProcessedBy);
    }

    [Fact]
    public void CreateClaim_rejects_active_coverage_that_has_not_started_without_changes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            seedContext.Products.Add(new Product
            {
                Id = 1002,
                ProductCode = "P1002",
                DisplayName = "Future warranty product",
                CategoryId = 1,
                BrandId = 1,
                DefaultUnitId = 1,
                DefaultPrice = 10m,
                IsSerialTracked = true
            });
            var serial = new ProductSerial
            {
                SerialNumber = "WARRANTY-FUTURE",
                ProductId = 1002,
                CurrentStatus = SerialStatus.Sold.ToString()
            };
            seedContext.ProductSerials.Add(serial);
            seedContext.SaveChanges();
            seedContext.WarrantyCoverages.Add(new WarrantyCoverage
            {
                ProductSerialId = serial.Id,
                CustomerId = 1,
                WarrantyStartDate = DateTime.Today.AddDays(1),
                WarrantyEndDate = DateTime.Today.AddDays(30),
                CoverageStatus = "Active"
            });
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        Assert.Null(service.GetCoverageBySerial("WARRANTY-FUTURE"));
        Assert.Throws<InventoryDomainException>(() =>
            service.CreateClaim(
                "WC-FUTURE",
                "WARRANTY-FUTURE",
                "Not started",
                userId: 1));

        using var assertContext = CreateContext(connection);
        Assert.Empty(assertContext.WarrantyClaims);
        Assert.Equal(
            SerialStatus.Sold.ToString(),
            assertContext.ProductSerials.Single().CurrentStatus);
    }

    [Fact]
    public void CreateClaim_rejects_second_open_claim_for_same_serial()
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

        var error = Assert.Throws<InventoryDomainException>(() =>
            service.CreateClaim("WC-0002", "WARRANTY-002", "Battery issue", userId: 1));

        Assert.Contains("đang có phiếu bảo hành chưa kết thúc", error.Message);
        using var assertContext = CreateContext(connection);
        Assert.Single(assertContext.WarrantyClaims);
        Assert.DoesNotContain(assertContext.WarrantyClaims, claim => claim.ClaimCode == "WC-0002");
    }

    [Theory]
    [InlineData("Closed")]
    [InlineData("Rejected")]
    public void CreateClaim_allows_new_claim_after_previous_terminal_status(string terminalStatus)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            var oldClaimId = SeedOpenClaim(seedContext, "WARRANTY-TERMINAL", $"WC-{terminalStatus}");
            var oldClaim = seedContext.WarrantyClaims.Single(item => item.Id == oldClaimId);
            oldClaim.Status = terminalStatus;
            seedContext.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));
        var newClaimId = service.CreateClaim(
            $"WC-NEW-{terminalStatus}",
            "WARRANTY-TERMINAL",
            "New issue",
            userId: 1);

        using var assertion = CreateContext(connection);
        Assert.Equal(2, assertion.WarrantyClaims.Count());
        Assert.Equal("Open", assertion.WarrantyClaims.Single(item => item.Id == newClaimId).Status);
    }

    [Fact]
    public void CreateClaim_rejects_duplicate_code_before_write()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using (var seedContext = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seedContext);
            SeedOpenClaim(seedContext, "WARRANTY-DUP-CODE", "WC-DUP");
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        var exception = Assert.Throws<InventoryDomainException>(
            () => service.CreateClaim("WC-DUP", "WARRANTY-DUP-CODE", "Duplicate code", userId: 1));

        Assert.Contains("đã tồn tại", exception.Message);
        Assert.Null(exception.InnerException);
        using var assertion = CreateContext(connection);
        Assert.Single(assertion.WarrantyClaims);
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
        var ex = Assert.Throws<InventoryDomainException>(() => service.DeleteClaim(claimId));
        Assert.Contains("Không thể xóa phiếu bảo hành", ex.Message);
    }

    [Fact]
    public void DeleteClaim_restores_serial_status_when_only_open_claim_is_removed()
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
            seedContext.WarrantyClaims.Add(claim1);
            seedContext.SaveChanges();
            claimId1 = claim1.Id;
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        service.DeleteClaim(claimId1);

        using (var assertContext = CreateContext(connection))
        {
            var serial = assertContext.ProductSerials.Find(serialId);
            Assert.NotNull(serial);
            Assert.Equal("Sold", serial.CurrentStatus);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task ReplaceSerialAsync_rolls_back_claim_serial_stockout_and_audit_on_late_failure()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(connection, "Ready", twoReplacementSerials: false);

        ReplacementSnapshot before;
        byte[] rowVersion;
        int auditCount;
        using (var snapshotContext = CreateContext(connection))
        {
            before = ReadReplacementSnapshot(snapshotContext, claimId);
            rowVersion = snapshotContext.WarrantyClaims
                .Single(item => item.Id == claimId)
                .RowVersion
                .ToArray();
            auditCount = snapshotContext.AuditLogs.Count();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailWarrantyClaimClose
                BEFORE UPDATE OF Status ON WarrantyClaim
                WHEN NEW.Status = 'Closed'
                BEGIN
                    SELECT RAISE(ABORT, 'forced late warranty failure');
                END;
                """;
            command.ExecuteNonQuery();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            service.ReplaceSerialAsync(
                claimId,
                "REPLACEMENT-1",
                "late failure",
                rowVersion,
                userId: 4,
                Guid.NewGuid()));

        using var assertion = CreateContext(connection);
        Assert.Equal(before, ReadReplacementSnapshot(assertion, claimId));
        Assert.Equal(auditCount, assertion.AuditLogs.Count());
        Assert.Equal("Ready", assertion.WarrantyClaims.Single(item => item.Id == claimId).Status);
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
                Status = "Ready",
                ResolutionType = "Replace"
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
    public void ReplaceSerial_transfers_coverage_ending_today_for_the_same_day()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(
            connection,
            "Ready",
            twoReplacementSerials: false,
            coverageEndDate: DateTime.Today);
        var service = new WarrantyClaimService(() => CreateContext(connection));

        service.ReplaceSerial(claimId, "REPLACEMENT-1", "Same-day replacement", userId: 4);

        using var assertContext = CreateContext(connection);
        var claim = assertContext.WarrantyClaims.Single(item => item.Id == claimId);
        var transferred = assertContext.WarrantyCoverages.Single(
            coverage => coverage.ProductSerialId == claim.ReplacementSerialId);
        Assert.Equal("Active", transferred.CoverageStatus);
        Assert.Equal(DateTime.Today, transferred.WarrantyStartDate.Date);
        Assert.Equal(DateTime.Today, transferred.WarrantyEndDate.Date);
    }

    [Fact]
    public void ReplaceSerial_rejects_ready_repair_claim_without_changes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(
            connection,
            "Ready",
            twoReplacementSerials: false,
            resolutionType: "Repair");
        var service = new WarrantyClaimService(() => CreateContext(connection));
        ReplacementSnapshot before;
        using (var snapshotContext = CreateContext(connection))
        {
            before = ReadReplacementSnapshot(snapshotContext, claimId);
        }

        Assert.Throws<InvalidOperationException>(() =>
            service.ReplaceSerial(claimId, "REPLACEMENT-1", "Invalid replacement", userId: 4));

        using var assertContext = CreateContext(connection);
        Assert.Equal(before, ReadReplacementSnapshot(assertContext, claimId));
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

    [Fact]
    public void UpdateClaim_cannot_forge_replacement_approval_or_transition_owned_fields()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var claimId = SeedReplacementClaim(
            connection,
            "Ready",
            twoReplacementSerials: false,
            resolutionType: "Repair");
        WarrantyClaim detached;
        int replacementSerialId;
        using (var arrangeContext = CreateContext(connection))
        {
            detached = arrangeContext.WarrantyClaims
                .AsNoTracking()
                .Single(item => item.Id == claimId);
            replacementSerialId = arrangeContext.ProductSerials
                .Single(item => item.SerialNumber == "REPLACEMENT-1")
                .Id;
        }
        detached.ProblemDescription = "Updated symptom";
        detached.ResolutionType = "Replace";
        detached.ApprovedBy = 4;
        detached.ClosedDate = DateTime.Today;
        detached.ReplacementSerialId = replacementSerialId;

        var service = new WarrantyClaimService(() => CreateContext(connection));
        service.UpdateClaim(detached);

        using var assertContext = CreateContext(connection);
        var stored = assertContext.WarrantyClaims.Single(item => item.Id == claimId);
        Assert.Equal("Updated symptom", stored.ProblemDescription);
        Assert.Equal("Repair", stored.ResolutionType);
        Assert.Null(stored.ApprovedBy);
        Assert.Null(stored.ClosedDate);
        Assert.Null(stored.ReplacementSerialId);
    }

    [Fact]
    public void WarrantyClaimService_does_not_expose_raw_entity_creation()
    {
        var unsafeOverload = typeof(WarrantyClaimService)
            .GetMethods()
            .SingleOrDefault(method =>
                method.Name == nameof(WarrantyClaimService.CreateClaim)
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(WarrantyClaim));

        Assert.Null(unsafeOverload);
    }

    [Fact]
    public async Task CreateClaimAsync_canonicalizes_lowercase_serial_input()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var arrange = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(arrange);
            arrange.AppUsers.Add(new AppUser { Id = 4, Username = "w1", FullName = "W1", PasswordHash = "h", RoleCode = "Nhân viên bảo hành", IsActive = true });
            arrange.ProductSerials.Add(new ProductSerial { Id = 88, ProductId = 1, SerialNumber = "SN-WARRANTY-001", CurrentStatus = "Sold", CurrentWarehouseId = null, LastStockInLineId = 1 });
            arrange.WarrantyCoverages.Add(new WarrantyCoverage { ProductSerialId = 88, CustomerId = 1, CoverageStatus = "Active", WarrantyStartDate = DateTime.Today.AddDays(-10), WarrantyEndDate = DateTime.Today.AddDays(30) });
            arrange.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));
        var claimId = await service.CreateClaimAsync("CLM-LOWER-01", "sn-warranty-001", "Screen broken", 4, Guid.NewGuid());

        using var assert = CreateContext(connection);
        var claim = assert.WarrantyClaims.Single(c => c.Id == claimId);
        Assert.Equal(88, claim.ProductSerialId);
    }

    [Fact]
    public void GetCoverageBySerial_canonicalizes_lowercase_serial_input()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var arrange = CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(arrange);
            arrange.ProductSerials.Add(new ProductSerial { Id = 99, ProductId = 1, SerialNumber = "SN-COVERAGE-001", CurrentStatus = "Sold", CurrentWarehouseId = null, LastStockInLineId = 1 });
            arrange.WarrantyCoverages.Add(new WarrantyCoverage { ProductSerialId = 99, CustomerId = 1, CoverageStatus = "Active", WarrantyStartDate = DateTime.Today.AddDays(-10), WarrantyEndDate = DateTime.Today.AddDays(30) });
            arrange.SaveChanges();
        }

        var service = new WarrantyClaimService(() => CreateContext(connection));
        var coverage = service.GetCoverageBySerial("sn-coverage-001");

        Assert.NotNull(coverage);
        Assert.Equal(99, coverage.ProductSerialId);
    }

    private static int SeedReplacementClaim(
        SqliteConnection connection,
        string status,
        bool twoReplacementSerials,
        string? resolutionType = null,
        DateTime? coverageEndDate = null)
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
            WarrantyEndDate = coverageEndDate ?? DateTime.Now.AddDays(15),
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
            Status = status,
            ResolutionType = resolutionType ?? (status == "Ready" ? "Replace" : null)
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

