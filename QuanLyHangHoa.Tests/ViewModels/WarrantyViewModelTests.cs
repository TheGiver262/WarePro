using QuanLyHangHoa.Models;
using QuanLyHangHoa.ViewModels;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Tests.ViewModels
{
    public class WarrantyViewModelTests
    {
        [Fact]
        public async Task CreateWarrantyClaimPassesFormValuesToService()
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            
            using (var seedContext = DatabaseHelper.CreateContext(connection))
            {
                DatabaseHelper.SeedBasicData(seedContext);
                seedContext.Products.Add(new Product 
                { 
                    Id = 1000, 
                    ProductCode = "P1000",
                    DisplayName = "Warranty product",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsSerialTracked = true
                });
                var serial = new ProductSerial 
                { 
                    Id = 500,
                    SerialNumber = "SERIAL-001",
                    ProductId = 1000,
                    CurrentStatus = SerialStatus.Sold.ToString()
                };
                seedContext.ProductSerials.Add(serial);
                seedContext.SaveChanges();
                
                seedContext.WarrantyCoverages.Add(new WarrantyCoverage
                {
                    ProductSerialId = serial.Id,
                    CustomerId = 1,
                    WarrantyStartDate = DateTime.Today.AddHours(23),
                    WarrantyEndDate = DateTime.Today.AddDays(30),
                    CoverageStatus = "Active"
                });
                seedContext.SaveChanges();
            }

            var viewModel = new WarrantyViewModel(
                new AppUser { Id = 1, FullName = "Nhan vien" },
                () => DatabaseHelper.CreateContext(connection),
                (msg, title) => { });

            viewModel.LoadAvailableSerials();
            Assert.Contains("SERIAL-001", viewModel.AvailableSerials);

            viewModel.ClaimCode = "WC-001";
            viewModel.SerialNumber = "SERIAL-001";
            viewModel.ProblemDescription = "Loi man hinh";

            await viewModel.CreateWarrantyClaimCommand.ExecuteAsync(null);

            using (var assertContext = DatabaseHelper.CreateContext(connection))
            {
                var claim = Assert.Single(assertContext.WarrantyClaims);
                Assert.Equal("WC-001", claim.ClaimCode);
                Assert.Equal("Loi man hinh", claim.ProblemDescription);
                Assert.Equal(1, claim.ProcessedBy);
                Assert.Equal("InWarrantyProcess", assertContext.ProductSerials.First(s => s.Id == 500).CurrentStatus);
            }

            Assert.StartsWith("Đã tạo phiếu bảo hành", viewModel.StatusMessage);
            Assert.Equal(string.Empty, viewModel.SerialNumber);
            Assert.Equal(string.Empty, viewModel.ProblemDescription);
        }

        
        [Fact]
        public async Task CreateWarrantyClaim_hides_untrusted_provider_exception_detail()
        {
            var messages = new List<string>();
            var viewModel = new WarrantyViewModel(
                new AppUser { Id = 1 },
                () => throw new InvalidOperationException("Server=secret; SQL syntax near password"),
                (message, _) => messages.Add(message));
            viewModel.ClaimCode = "WC-SECRET";
            viewModel.SerialNumber = "SERIAL-SECRET";
            viewModel.ProblemDescription = "Provider failure";

            await viewModel.CreateWarrantyClaimCommand.ExecuteAsync(null);

            Assert.Equal(DatabaseWriteUi.TechnicalErrorMessage, viewModel.StatusMessage);
            Assert.Equal([DatabaseWriteUi.TechnicalErrorMessage], messages);
            Assert.DoesNotContain("secret", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SQL", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        }[Fact]
        public async Task CompleteRepairPassesClaimIdConclusionAndCurrentUserToService()
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();

            int claimId;
            using (var seedContext = DatabaseHelper.CreateContext(connection))
            {
                DatabaseHelper.SeedBasicData(seedContext);
                
                seedContext.Products.Add(new Product 
                { 
                    Id = 2000, 
                    ProductCode = "P2000",
                    DisplayName = "Warranty product",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsSerialTracked = true
                });
                var serial = new ProductSerial 
                { 
                    Id = 600,
                    SerialNumber = "SERIAL-002",
                    ProductId = 2000,
                    CurrentStatus = "InWarrantyProcess"
                };
                seedContext.ProductSerials.Add(serial);
                seedContext.SaveChanges();
                
                var coverage = new WarrantyCoverage
                {
                    Id = 100,
                    ProductSerialId = serial.Id,
                    CustomerId = 1,
                    WarrantyStartDate = new DateTime(2026, 1, 1),
                    WarrantyEndDate = new DateTime(2027, 1, 1),
                    CoverageStatus = "Active"
                };
                seedContext.WarrantyCoverages.Add(coverage);
                seedContext.SaveChanges();

                var claim = new WarrantyClaim
                {
                    Id = 9,
                    ClaimCode = "WC-002",
                    WarrantyCoverageId = coverage.Id,
                    ProductSerialId = serial.Id,
                    ReceivedDate = new DateTime(2026, 4, 28),
                    Status = "Open",
                    ProblemDescription = "Faulty motherboard"
                };
                seedContext.WarrantyClaims.Add(claim);
                seedContext.SaveChanges();
                claimId = claim.Id;
            }

            var viewModel = new WarrantyViewModel(
                new AppUser { Id = 1 },
                () => DatabaseHelper.CreateContext(connection),
                (msg, title) => { });

            using (var context = DatabaseHelper.CreateContext(connection))
            {
                var claim = context.WarrantyClaims.Find(claimId);
                viewModel.SelectedWarranty = claim;
            }

            viewModel.TechnicalConclusion = "Fixed screen";
            await viewModel.CompleteRepairCommand.ExecuteAsync(null);

            using (var assertContext = DatabaseHelper.CreateContext(connection))
            {
                var claim = assertContext.WarrantyClaims.Find(claimId);
                Assert.NotNull(claim);
                Assert.Equal("Ready", claim.Status);
                Assert.Equal("Fixed screen", claim.TechnicalConclusion);
                Assert.Equal(1, claim.ApprovedBy);
                
                var serial = assertContext.ProductSerials.Find(claim.ProductSerialId);
                Assert.NotNull(serial);
                Assert.Equal("Sold", serial.CurrentStatus);
            }

            Assert.Equal("Đã hoàn tất sửa bảo hành.", viewModel.StatusMessage);
        }

        [Fact]
        public async Task CoverageList_treats_expired_stored_active_coverage_as_expired()
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            using (var seedContext = DatabaseHelper.CreateContext(connection))
            {
                DatabaseHelper.SeedBasicData(seedContext);
                seedContext.Products.Add(new Product
                {
                    Id = 3000,
                    ProductCode = "P3000",
                    DisplayName = "Expired warranty product",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 10m,
                    IsSerialTracked = true
                });
                var serial = new ProductSerial
                {
                    SerialNumber = "SERIAL-EXPIRED",
                    ProductId = 3000,
                    CurrentStatus = SerialStatus.Sold.ToString()
                };
                seedContext.ProductSerials.Add(serial);
                seedContext.SaveChanges();
                seedContext.WarrantyCoverages.Add(new WarrantyCoverage
                {
                    ProductSerialId = serial.Id,
                    CustomerId = 1,
                    WarrantyStartDate = DateTime.Today.AddYears(-1),
                    WarrantyEndDate = DateTime.Today.AddDays(-1),
                    CoverageStatus = "Active"
                });
                seedContext.SaveChanges();
            }

            var viewModel = new WarrantyCoverageViewModel(
                () => DatabaseHelper.CreateContext(connection));
            await viewModel.LoadData();

            var displayed = Assert.Single(viewModel.Coverages);
            Assert.Equal("Active", displayed.CoverageStatus);
            Assert.Equal("Expired", displayed.EffectiveCoverageStatus);
            Assert.Equal(0, viewModel.ActiveCount);
            Assert.Equal(1, viewModel.ExpiredCount);

            displayed.WarrantyEndDate = DateTime.Today.AddDays(30);
            using (var updateContext = DatabaseHelper.CreateContext(connection))
            {
                updateContext.Attach(displayed);
                updateContext.Entry(displayed).Property(item => item.WarrantyEndDate).IsModified = true;
                updateContext.Entry(displayed).Property(item => item.CoverageStatus).IsModified = true;
                updateContext.SaveChanges();
            }

            using (var assertContext = DatabaseHelper.CreateContext(connection))
            {
                Assert.Equal("Active", assertContext.WarrantyCoverages.Single().CoverageStatus);
            }
        }

        [Fact]
        public async Task Claim_date_filter_includes_the_whole_selected_end_day()
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            var selectedDate = new DateTime(2026, 8, 13);
            using (var db = DatabaseHelper.CreateContext(connection))
            {
                DatabaseHelper.SeedBasicData(db);
                db.Products.Add(new Product
                {
                    Id = 3701,
                    ProductCode = "DATE-PRODUCT",
                    DisplayName = "Date filter product",
                    CategoryId = 1,
                    BrandId = 1,
                    DefaultUnitId = 1,
                    DefaultPrice = 1m,
                    IsActive = true,
                    IsSerialTracked = true
                });
                foreach (var id in Enumerable.Range(701, 4))
                {
                    db.ProductSerials.Add(new ProductSerial
                    {
                        Id = id,
                        SerialNumber = $"DATE-SERIAL-{id}",
                        ProductId = 3701,
                        CurrentStatus = SerialStatus.InWarrantyProcess.ToString()
                    });
                    db.WarrantyCoverages.Add(new WarrantyCoverage
                    {
                        Id = id,
                        ProductSerialId = id,
                        CustomerId = 1,
                        WarrantyStartDate = selectedDate.AddYears(-1),
                        WarrantyEndDate = selectedDate.AddYears(1),
                        CoverageStatus = "Active"
                    });
                }
                db.WarrantyClaims.AddRange(
                    CreateClaim(701, "DATE-START", selectedDate),
                    CreateClaim(702, "DATE-LATE", selectedDate.AddHours(23).AddMinutes(59)),
                    CreateClaim(703, "DATE-NEXT", selectedDate.AddDays(1)),
                    CreateClaim(704, "DATE-AFTER", selectedDate.AddDays(2)));
                db.SaveChanges();
            }
            var viewModel = new WarrantyViewModel(
                new AppUser { Id = 1 },
                () => DatabaseHelper.CreateContext(connection),
                (_, _) => { })
            {
                SearchFromDate = selectedDate.AddHours(14),
                SearchToDate = selectedDate.AddHours(10)
            };

            await viewModel.LoadData();

            Assert.Equal(["DATE-LATE", "DATE-START"], viewModel.Warranties.Select(item => item.ClaimCode));
        }

        private static WarrantyClaim CreateClaim(int id, string code, DateTime receivedDate) => new()
        {
            Id = id,
            ClaimCode = code,
            WarrantyCoverageId = id,
            ProductSerialId = id,
            ReceivedDate = receivedDate,
            Status = "Open",
            ProcessedBy = 1
        };

        [Theory]
        [InlineData("Open", true, true, false, false, true, false, true)]
        [InlineData("ManufacturerWait", false, false, true, true, false, false, true)]
        [InlineData("Ready", false, false, false, false, false, true, true)]
        [InlineData("Closed", false, false, false, false, false, false, false)]
        [InlineData("Rejected", false, false, false, false, false, false, false)]
        public void Warranty_commands_mirror_transition_policy(
            string status,
            bool canCompleteRepair,
            bool canSendManufacturer,
            bool canReceiveManufacturerRepaired,
            bool canReceiveManufacturerReplaced,
            bool canReject,
            bool canReplaceFromStock,
            bool canEdit)
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            var viewModel = new WarrantyViewModel(
                new AppUser { Id = 1 },
                () => DatabaseHelper.CreateContext(connection),
                (message, title) => { });

            viewModel.SelectedWarranty = new WarrantyClaim
            {
                Id = 1,
                ClaimCode = "WC-STATE",
                WarrantyCoverageId = 1,
                ProductSerialId = 1,
                ReceivedDate = DateTime.Today,
                Status = status,
                ResolutionType = status == "Ready" ? "Replace" : null
            };

            Assert.Equal(canCompleteRepair, viewModel.CompleteRepairCommand.CanExecute(null));
            Assert.Equal(canSendManufacturer, viewModel.SendManufacturerCommand.CanExecute(null));
            Assert.Equal(canReceiveManufacturerRepaired, viewModel.ReceiveManufacturerRepairedCommand.CanExecute(null));
            Assert.Equal(canReceiveManufacturerReplaced, viewModel.ReceiveManufacturerReplacedCommand.CanExecute(null));
            Assert.Equal(canReject, viewModel.RejectWarrantyCommand.CanExecute(null));
            Assert.Equal(canReplaceFromStock, viewModel.ReplaceWarrantySerialCommand.CanExecute(null));
            Assert.Equal(canEdit, viewModel.SaveWarrantyCommand.CanExecute(null));
            Assert.Equal(canEdit, viewModel.DeleteWarrantyCommand.CanExecute(null));
        }

        [Fact]
        public void Ready_repair_claim_cannot_be_replaced_from_stock()
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
            connection.Open();
            var viewModel = new WarrantyViewModel(
                new AppUser { Id = 1 },
                () => DatabaseHelper.CreateContext(connection),
                (message, title) => { });
            viewModel.SelectedWarranty = new WarrantyClaim
            {
                Status = "Ready",
                ResolutionType = "Repair"
            };

            Assert.False(viewModel.ReplaceWarrantySerialCommand.CanExecute(null));
        }

        [Fact]
        public void WarrantyView_binds_transition_visibility_and_terminal_read_only_state()
        {
            var repoRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var xaml = File.ReadAllText(
                Path.Combine(repoRoot, "QuanLyHangHoa", "Views", "WarrantyView.xaml"));

            Assert.Contains(
                "IsEnabled=\"{Binding IsSelectedWarrantyMutable}\"",
                xaml);
            Assert.Contains("Visibility=\"{Binding CanCompleteRepair,", xaml);
            Assert.Contains("Visibility=\"{Binding CanSendManufacturer,", xaml);
            Assert.Contains(
                "Visibility=\"{Binding CanReceiveManufacturerActions,",
                xaml);
            Assert.Contains(
                "Visibility=\"{Binding CanReplaceWarrantySerial,",
                xaml);
            Assert.Contains("Visibility=\"{Binding CanRejectWarranty,", xaml);
        }

        [Fact]
        public void WarrantyCoverageView_binds_effective_status_without_replacing_stored_status()
        {
            var repoRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            var xaml = File.ReadAllText(
                Path.Combine(repoRoot, "QuanLyHangHoa", "Views", "WarrantyCoverageView.xaml"));

            Assert.Contains("SortMemberPath=\"EffectiveCoverageStatus\"", xaml);
            Assert.Contains("{Binding EffectiveCoverageStatus,", xaml);
        }
    }
}
