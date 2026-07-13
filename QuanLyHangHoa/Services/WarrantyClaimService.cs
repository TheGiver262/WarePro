using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class WarrantyClaimService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public WarrantyClaimService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public static void EnsureValidCoverageDates(DateTime startDate, DateTime endDate)
        {
            if (endDate.Date < startDate.Date)
            {
                throw new InvalidOperationException(
                    "Warranty end date cannot be before warranty start date.");
            }
        }

        public static string GetEffectiveCoverageStatus(
            string storedStatus,
            DateTime warrantyEndDate,
            DateTime asOfDate)
        {
            return string.Equals(storedStatus, "Active", StringComparison.OrdinalIgnoreCase)
                && warrantyEndDate.Date < asOfDate.Date
                    ? "Expired"
                    : storedStatus;
        }

        public void CreateClaim(WarrantyClaim claim)
        {
            using var db = _contextFactory();
            db.WarrantyClaims.Add(claim);
            db.SaveChanges();
        }

        public void UpdateClaim(WarrantyClaim claim)
        {
            using var db = _contextFactory();
            var existing = db.WarrantyClaims.Find(claim.Id)
                ?? throw new InvalidOperationException($"Claim {claim.Id} not found.");
            WarrantyClaimTransitions.EnsureMutable(existing.Status);
            if (!string.Equals(existing.Status, claim.Status, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Warranty claim status must be changed through a warranty action.");
            }

            db.Entry(existing).CurrentValues.SetValues(claim);
            db.SaveChanges();
        }

        public void ResolveClaim(int claimId, string resolutionType, string technicalConclusion, int approverId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Resolve);
            claim.ResolutionType = resolutionType;
            claim.TechnicalConclusion = technicalConclusion;
            claim.ApprovedBy = approverId;
            claim.Status = "Ready";
            db.SaveChanges();
        }

        public void CloseClaim(int claimId, string note)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Close);
            claim.ProcessingNote = note;
            claim.Status = "Closed";
            claim.ClosedDate = DateTime.Now;

            // Update serial status based on other open claims
            UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);

            db.SaveChanges();
        }

        /// <summary>
        /// Creates a new warranty claim from serial number lookup.
        /// Sets serial status to InWarrantyProcess.
        /// </summary>
        public int CreateClaim(string claimCode, string serialNumber, string problemDescription, int userId)
        {
            using var db = _contextFactory();
            var serial = db.ProductSerials.FirstOrDefault(s => s.SerialNumber == serialNumber)
                ?? throw new InvalidOperationException($"Serial {serialNumber} không tồn tại.");

            var coverage = db.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == serial.Id && c.CoverageStatus == "Active" && c.WarrantyEndDate >= DateTime.Today)
                ?? throw new InvalidOperationException($"Serial {serialNumber} không có bảo hành còn hiệu lực.");

            var claim = new WarrantyClaim
            {
                ClaimCode = claimCode,
                ProductSerialId = serial.Id,
                WarrantyCoverageId = coverage.Id,
                ProblemDescription = problemDescription,
                ReceivedDate = DateTime.Now,
                Status = "Open",
                ProcessedBy = userId
            };

            // Mark serial as in warranty process if not already
            if (serial.CurrentStatus != "InWarrantyProcess" && serial.CurrentStatus != "ReturnedToManufacturer")
            {
                serial.CurrentStatus = "InWarrantyProcess";
            }

            db.WarrantyClaims.Add(claim);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException(
                    "Không thể tạo phiếu bảo hành. Vui lòng kiểm tra mã phiếu và dữ liệu bảo hành đã tồn tại.",
                    ex);
            }

            return claim.Id;
        }

        public void CompleteRepair(int claimId, string technicalConclusion, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Repair);
            if (!string.Equals(claim.Status, "Open", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Store repair completion is allowed only for an open warranty claim.");
            }

            claim.TechnicalConclusion = technicalConclusion;
            claim.Status = "Ready";
            claim.ResolutionType = "Repair";
            claim.ApprovedBy = userId;

            // Update serial status based on other open claims
            UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);

            db.SaveChanges();
        }

        /// <summary>
        /// Send defective item to manufacturer for warranty processing.
        /// Sets serial status to ReturnedToManufacturer and populates tracking fields.
        /// </summary>
        public void SendToManufacturer(int claimId, string manufacturerName, string trackingCode,
            DateTime? expectedReturnDate, string note, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Send);
            claim.ManufacturerName = manufacturerName;
            claim.ManufacturerTrackingCode = trackingCode;
            claim.ManufacturerExpectedReturnDate = expectedReturnDate;
            claim.ManufacturerResult = note;
            claim.Status = "ManufacturerWait";
            claim.ProcessedBy = userId;

            // Mark serial as returned to manufacturer
            var serial = db.ProductSerials.Find(claim.ProductSerialId);
            if (serial != null) serial.CurrentStatus = "ReturnedToManufacturer";

            db.SaveChanges();
        }

        /// <summary>
        /// Backward-compatible overload (used by existing ViewModel delegates).
        /// </summary>
        public void SendToManufacturer(int claimId, string manufacturerNote, int userId)
        {
            SendToManufacturer(claimId, null!, null!, null, manufacturerNote, userId);
        }

        /// <summary>
        /// Receive repaired item from manufacturer (same serial).
        /// Marks serial as Sold (returned to customer).
        /// </summary>
        public void ReceiveFromManufacturerRepaired(int claimId, string conclusion, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Repair);
            if (claim.Status != "ManufacturerWait")
                throw new InvalidOperationException("Phiếu bảo hành này chưa được gửi hãng.");

            claim.TechnicalConclusion = conclusion;
            claim.ResolutionType = "ManufacturerRepair";
            claim.Status = "Closed";
            claim.ClosedDate = DateTime.Now;
            claim.ApprovedBy = userId;

            // Update serial status based on other open claims
            UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);

            db.SaveChanges();
        }

        /// <summary>
        /// Receive replacement item from manufacturer (new serial).
        /// - Marks defective serial as Replaced.
        /// - Creates StockIn for new serial (PurposeCode = WarrantyReceive, cost = 0).
        /// - Creates StockOut for new serial (PurposeCode = WarrantyReplace, WarrantyReplacement kind).
        /// - Both documents are auto-posted.
        /// </summary>
        public void ReceiveFromManufacturerReplaced(int claimId, string newSerialNumber,
            string conclusion, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(
                System.Data.IsolationLevel.Serializable);

            var claim = db.WarrantyClaims
                .Include(c => c.ProductSerial)
                .ThenInclude(s => s.Product)
                .Include(c => c.WarrantyCoverage)
                .FirstOrDefault(c => c.Id == claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Replace);
            if (claim.Status != "ManufacturerWait")
                throw new InvalidOperationException("Phiếu bảo hành này chưa được gửi hãng.");

            EnsureReplacementNotApplied(claim);

            var defectiveSerial = claim.ProductSerial;
            var product = defectiveSerial.Product;
            var customerId = claim.WarrantyCoverage.CustomerId;

            var warehouseId = GetDefaultWarehouseId(db);

            var unitId = GetBaseUnitId(db, product);

            // 1. Mark defective serial as Replaced
            defectiveSerial.CurrentStatus = "Replaced";

            // 2. Create StockIn for new serial (WarrantyReceive, cost = 0, auto-posted)
            var stockIn = new StockIn
            {
                DocumentCode = $"WRI-{DateTime.Now:yyyyMMddHHmmss}",
                WarehouseId = warehouseId,
                PurposeCode = "WarrantyReceive",
                Status = StockDocumentStatus.Approved.ToString(),
                ImportDate = DateTime.Now,
                Notes = $"Nhận serial mới từ hãng BH cho claim #{claim.ClaimCode}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
                ApprovedBy = userId,
                ApprovedAt = DateTime.Now,
                PostedBy = userId,
                PostedAt = DateTime.Now,
                Lines = new List<StockInLine>
                {
                    new StockInLine
                    {
                        ProductId = product.Id,
                        UnitId = unitId,
                        Quantity = 1,
                        BaseQuantity = 1,
                        UnitPrice = 0
                    }
                }
            };
            db.StockIns.Add(stockIn);
            db.SaveChanges();

            // Post inventory for StockIn
            var postingService = CreatePostingService(db);

            postingService.PostStockIn(new PostStockInCommand(
                stockIn.Id,
                warehouseId,
                StockInKind.WarrantyReceive,
                StockDocumentStatus.Approved,
                product.Id,
                1,
                new[] { newSerialNumber },
                userId));

            // 3. Create StockOut for new serial (WarrantyReplace, auto-posted)
            var newSerial = db.ProductSerials.FirstOrDefault(s => s.SerialNumber == newSerialNumber)
                ?? throw new InvalidOperationException($"Serial mới {newSerialNumber} không tìm thấy sau khi nhập kho.");

            var stockOut = new StockOut
            {
                DocumentCode = $"WRO-{DateTime.Now:yyyyMMddHHmmss}",
                CustomerId = customerId,
                WarehouseId = warehouseId,
                PurposeCode = "WarrantyReplacement",
                Status = StockDocumentStatus.Approved.ToString(),
                ExportDate = DateTime.Now,
                Notes = $"Xuất serial mới BH cho claim #{claim.ClaimCode}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
                ApprovedBy = userId,
                ApprovedAt = DateTime.Now,
                PostedBy = userId,
                PostedAt = DateTime.Now,
                Lines = new List<StockOutLine>
                {
                    new StockOutLine
                    {
                        ProductId = product.Id,
                        UnitId = unitId,
                        Quantity = 1,
                        BaseQuantity = 1,
                        UnitPrice = 0,
                        ProductSerials = new List<ProductSerial> { newSerial }
                    }
                }
            };
            db.StockOuts.Add(stockOut);
            db.SaveChanges();

            postingService.PostStockOut(new PostStockOutCommand(
                stockOut.Id,
                warehouseId,
                StockOutKind.WarrantyReplacement,
                StockDocumentStatus.Approved,
                product.Id,
                1,
                new[] { newSerialNumber },
                userId));

            // 4. Update Warranty Coverage
            TransferRemainingCoverage(db, claim.WarrantyCoverage, newSerial.Id);

            // 5. Update claim
            claim.TechnicalConclusion = conclusion;
            claim.ResolutionType = "ManufacturerReplace";
            claim.ReplacementSerialId = newSerial.Id;
            claim.ReplacementStockOutId = stockOut.Id;
            claim.Status = "Closed";
            claim.ClosedDate = DateTime.Now;
            claim.ApprovedBy = userId;

            db.SaveChanges();
            transaction.Commit();
        }

        public void RejectClaim(int claimId, string reason, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Reject);
            claim.RejectionReason = reason;
            claim.Status = "Rejected";
            claim.ResolutionType = "Reject";
            claim.ApprovedBy = userId;
            claim.ClosedDate = DateTime.Now;

            // Update serial status based on other open claims
            UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);

            db.SaveChanges();
        }

        /// <summary>
        /// Direct replacement from stock. Only allowed if the product is in stock.
        /// If out of stock, throws exception prompting the user to send to manufacturer.
        /// </summary>
        public void ReplaceSerial(int claimId, string replacementSerial, string conclusion, int userId)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction(
                System.Data.IsolationLevel.Serializable);

            var claim = db.WarrantyClaims
                .Include(c => c.ProductSerial)
                .ThenInclude(s => s.Product)
                .Include(c => c.WarrantyCoverage)
                .FirstOrDefault(c => c.Id == claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            WarrantyClaimTransitions.EnsureAllowed(claim.Status, WarrantyClaimAction.Replace);
            if (!string.Equals(claim.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Direct replacement is allowed only for an approved Ready claim.");
            }

            EnsureReplacementNotApplied(claim);

            var defectiveSerial = claim.ProductSerial;
            var product = defectiveSerial.Product;
            var customerId = claim.WarrantyCoverage.CustomerId;

            var warehouseId = GetDefaultWarehouseId(db);

            // Validate replacement serial exists and is in stock
            var newSerial = db.ProductSerials
                .FirstOrDefault(s => s.SerialNumber == replacementSerial && s.CurrentStatus == "InStock" && s.CurrentWarehouseId == warehouseId)
                ?? throw new InvalidOperationException(
                    $"Serial {replacementSerial} không có trong kho hoặc không ở trạng thái sẵn sàng. " +
                    $"Nếu hết hàng, vui lòng gửi về hãng để bảo hành đổi trả.");

            if (newSerial.ProductId != product.Id)
                throw new InvalidOperationException($"Serial {replacementSerial} không thuộc sản phẩm {product.DisplayName}.");

            var unitId = GetBaseUnitId(db, product);

            // Mark defective serial as Replaced
            defectiveSerial.CurrentStatus = "Replaced";

            // Create and post replacement StockOut
            var stockOut = new StockOut
            {
                DocumentCode = $"WRO-{DateTime.Now:yyyyMMddHHmmss}",
                CustomerId = customerId,
                WarehouseId = warehouseId,
                PurposeCode = "WarrantyReplacement",
                Status = StockDocumentStatus.Approved.ToString(),
                ExportDate = DateTime.Now,
                Notes = $"Đổi serial BH cho claim #{claim.ClaimCode}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
                ApprovedBy = userId,
                ApprovedAt = DateTime.Now,
                PostedBy = userId,
                PostedAt = DateTime.Now,
                Lines = new List<StockOutLine>
                {
                    new StockOutLine
                    {
                        ProductId = product.Id,
                        UnitId = unitId,
                        Quantity = 1,
                        BaseQuantity = 1,
                        UnitPrice = 0,
                        ProductSerials = new List<ProductSerial> { newSerial }
                    }
                }
            };
            db.StockOuts.Add(stockOut);
            db.SaveChanges();

            var postingService = CreatePostingService(db);

            postingService.PostStockOut(new PostStockOutCommand(
                stockOut.Id,
                warehouseId,
                StockOutKind.WarrantyReplacement,
                StockDocumentStatus.Approved,
                product.Id,
                1,
                new[] { replacementSerial },
                userId));

            // Update Warranty Coverage
            TransferRemainingCoverage(db, claim.WarrantyCoverage, newSerial.Id);

            // Update claim
            claim.ReplacementSerialId = newSerial.Id;
            claim.ReplacementStockOutId = stockOut.Id;
            claim.TechnicalConclusion = conclusion;
            claim.Status = "Closed";
            claim.ResolutionType = "Replace";
            claim.ApprovedBy = userId;
            claim.ClosedDate = DateTime.Now;

            db.SaveChanges();
            transaction.Commit();
        }

        public void DeleteClaim(int claimId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            WarrantyClaimTransitions.EnsureMutable(claim.Status);
            // Kiểm tra xem đã tạo chứng từ liên quan hay chưa
            bool hasRelatedStockIn = db.StockIns.Any(si => si.Notes != null && si.Notes.Contains(claim.ClaimCode));
            bool hasRelatedStockOut = db.StockOuts.Any(so => so.Notes != null && so.Notes.Contains(claim.ClaimCode)) || claim.ReplacementStockOutId.HasValue;

            if (hasRelatedStockIn || hasRelatedStockOut)
            {
                throw new InvalidOperationException("Không thể xóa phiếu bảo hành khi đã phát sinh chứng từ liên quan.");
            }

            var serialId = claim.ProductSerialId;
            db.WarrantyClaims.Remove(claim);
            db.SaveChanges();

            // Khôi phục serial nếu không còn phiếu bảo hành mở nào khác
            UpdateSerialStatusOnClaimClosure(db, serialId, claimId);
            db.SaveChanges();
        }

        private static void EnsureReplacementNotApplied(WarrantyClaim claim)
        {
            if (claim.ReplacementSerialId.HasValue || claim.ReplacementStockOutId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Warranty claim {claim.Id} already has a replacement.");
            }
        }

        private static int GetDefaultWarehouseId(AppDbContext db)
        {
            return new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId();
        }

        private static int GetBaseUnitId(AppDbContext db, Product product)
        {
            var unitId = db.ProductUnits
                .Where(productUnit => productUnit.ProductId == product.Id && productUnit.IsBaseUnit)
                .Select(productUnit => productUnit.UnitId)
                .FirstOrDefault();
            return unitId == 0 ? product.DefaultUnitId : unitId;
        }

        private static InventoryPostingService CreatePostingService(AppDbContext db)
        {
            return new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());
        }

        private static void TransferRemainingCoverage(
            AppDbContext db,
            WarrantyCoverage? oldCoverage,
            int newSerialId)
        {
            if (oldCoverage == null || oldCoverage.CoverageStatus != "Active")
            {
                return;
            }

            EnsureValidCoverageDates(oldCoverage.WarrantyStartDate, oldCoverage.WarrantyEndDate);
            oldCoverage.CoverageStatus = "Inactive";
            var now = DateTime.Now;
            var remainingDays = (oldCoverage.WarrantyEndDate - now).TotalDays;
            if (remainingDays <= 0)
            {
                return;
            }

            db.WarrantyCoverages.Add(new WarrantyCoverage
            {
                ProductSerialId = newSerialId,
                CustomerId = oldCoverage.CustomerId,
                SalesInvoiceId = oldCoverage.SalesInvoiceId,
                WarrantyStartDate = now,
                WarrantyEndDate = now.AddDays(remainingDays),
                CoverageStatus = "Active"
            });
        }

        private void UpdateSerialStatusOnClaimClosure(AppDbContext db, int productSerialId, int currentClaimId)
        {
            var serial = db.ProductSerials.Find(productSerialId);
            if (serial == null) return;

            var hasOtherOpenClaims = db.WarrantyClaims.Any(c => c.ProductSerialId == productSerialId && c.Status != "Closed" && c.Status != "Rejected" && c.Id != currentClaimId);
            if (!hasOtherOpenClaims)
            {
                if (serial.CurrentStatus == "InWarrantyProcess" || serial.CurrentStatus == "ReturnedToManufacturer")
                {
                    serial.CurrentStatus = "Sold";
                }
            }
            else
            {
                // Nếu vẫn còn phiếu mở khác, hãy kiểm tra xem có phiếu nào đang chờ hãng không
                var hasManufacturerWait = db.WarrantyClaims.Any(c => c.ProductSerialId == productSerialId && c.Status == "ManufacturerWait" && c.Id != currentClaimId);
                if (hasManufacturerWait)
                {
                    serial.CurrentStatus = "ReturnedToManufacturer";
                }
                else
                {
                    serial.CurrentStatus = "InWarrantyProcess";
                }
            }
        }

        public WarrantyCoverage? GetCoverageBySerial(string serialNumber)
        {
            using var db = _contextFactory();
            var serial = db.ProductSerials
                .FirstOrDefault(s => s.SerialNumber == serialNumber);

            if (serial == null) return null;

            return db.WarrantyCoverages
                .FirstOrDefault(c => c.ProductSerialId == serial.Id && c.CoverageStatus == "Active" && c.WarrantyEndDate >= DateTime.Today);
        }

        private sealed class DbDefaultWarehouseProvider : IDefaultWarehouseProvider
        {
            private readonly AppDbContext _context;
            public DbDefaultWarehouseProvider(AppDbContext context) => _context = context;

            public int GetDefaultWarehouseId()
            {
                return _context.Warehouses
                    .Where(w => w.IsDefault && w.IsActive)
                    .Select(w => w.Id)
                    .FirstOrDefault() switch { 0 => 1, var id => id };
            }
        }

        private sealed class SystemClock : IClock
        {
            public DateTime Now => DateTime.Now;
        }
    }
}
