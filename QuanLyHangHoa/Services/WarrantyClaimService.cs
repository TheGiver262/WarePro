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

        public void CreateClaim(WarrantyClaim claim)
        {
            using var db = _contextFactory();
            db.WarrantyClaims.Add(claim);
            db.SaveChanges();
        }

        public void UpdateClaim(WarrantyClaim claim)
        {
            using var db = _contextFactory();
            db.WarrantyClaims.Update(claim);
            db.SaveChanges();
        }

        public void ResolveClaim(int claimId, string resolutionType, string technicalConclusion, int approverId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

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

            var coverage = db.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == serial.Id && c.CoverageStatus == "Active" && c.WarrantyEndDate >= DateTime.Now)
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
            db.SaveChanges();
            return claim.Id;
        }

        public void CompleteRepair(int claimId, string technicalConclusion, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

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
            using var transaction = db.Database.BeginTransaction();

            var claim = db.WarrantyClaims
                .Include(c => c.ProductSerial)
                .ThenInclude(s => s.Product)
                .Include(c => c.WarrantyCoverage)
                .FirstOrDefault(c => c.Id == claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            if (claim.Status != "ManufacturerWait")
                throw new InvalidOperationException("Phiếu bảo hành này chưa được gửi hãng.");

            var defectiveSerial = claim.ProductSerial;
            var product = defectiveSerial.Product;
            var customerId = claim.WarrantyCoverage.CustomerId;

            // Get default warehouse
            var warehouseId = db.Warehouses
                .Where(w => w.IsDefault && w.IsActive)
                .Select(w => w.Id)
                .FirstOrDefault();
            if (warehouseId == 0) warehouseId = 1;

            // Get the product's default unit
            var unitId = db.ProductUnits
                .Where(pu => pu.ProductId == product.Id && pu.IsBaseUnit)
                .Select(pu => pu.UnitId)
                .FirstOrDefault();
            if (unitId == 0) unitId = product.DefaultUnitId;

            // 1. Mark defective serial as Replaced
            defectiveSerial.CurrentStatus = "Replaced";

            // 2. Create StockIn for new serial (WarrantyReceive, cost = 0, auto-posted)
            var stockIn = new StockIn
            {
                DocumentCode = $"WRI-{DateTime.Now:yyyyMMddHHmmss}",
                WarehouseId = warehouseId,
                PurposeCode = "WarrantyReceive",
                Status = DocumentStatus.Posted,
                ImportDate = DateTime.Now,
                Notes = $"Nhận serial mới từ hãng BH cho claim #{claim.ClaimCode}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
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
            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            postingService.PostStockIn(new PostStockInCommand(
                stockIn.Id,
                warehouseId,
                StockInKind.WarrantyReceive,
                StockDocumentStatus.Posted,
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
                Status = DocumentStatus.Posted,
                ExportDate = DateTime.Now,
                Notes = $"Xuất serial mới BH cho claim #{claim.ClaimCode}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
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
                StockDocumentStatus.Posted,
                product.Id,
                1,
                new[] { newSerialNumber },
                userId));

            // 4. Update Warranty Coverage
            var oldCoverage = claim.WarrantyCoverage;
            if (oldCoverage != null && oldCoverage.CoverageStatus == "Active")
            {
                oldCoverage.CoverageStatus = "Inactive";

                var remainingDays = (oldCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
                if (remainingDays > 0)
                {
                    var newCoverage = new WarrantyCoverage
                    {
                        ProductSerialId = newSerial.Id,
                        CustomerId = oldCoverage.CustomerId,
                        SalesInvoiceId = oldCoverage.SalesInvoiceId,
                        WarrantyStartDate = DateTime.Now,
                        WarrantyEndDate = DateTime.Now.AddDays(remainingDays),
                        CoverageStatus = "Active"
                    };
                    db.WarrantyCoverages.Add(newCoverage);
                }
            }

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
            using var transaction = db.Database.BeginTransaction();

            var claim = db.WarrantyClaims
                .Include(c => c.ProductSerial)
                .ThenInclude(s => s.Product)
                .Include(c => c.WarrantyCoverage)
                .FirstOrDefault(c => c.Id == claimId)
                ?? throw new InvalidOperationException($"Phiếu bảo hành #{claimId} không tồn tại.");

            var defectiveSerial = claim.ProductSerial;
            var product = defectiveSerial.Product;
            var customerId = claim.WarrantyCoverage.CustomerId;

            // Get default warehouse
            var warehouseId = db.Warehouses
                .Where(w => w.IsDefault && w.IsActive)
                .Select(w => w.Id)
                .FirstOrDefault();
            if (warehouseId == 0) warehouseId = 1;

            // Validate replacement serial exists and is in stock
            var newSerial = db.ProductSerials
                .FirstOrDefault(s => s.SerialNumber == replacementSerial && s.CurrentStatus == "InStock" && s.CurrentWarehouseId == warehouseId)
                ?? throw new InvalidOperationException(
                    $"Serial {replacementSerial} không có trong kho hoặc không ở trạng thái sẵn sàng. " +
                    $"Nếu hết hàng, vui lòng gửi về hãng để bảo hành đổi trả.");

            if (newSerial.ProductId != product.Id)
                throw new InvalidOperationException($"Serial {replacementSerial} không thuộc sản phẩm {product.DisplayName}.");

            // Get the product's default unit
            var unitId = db.ProductUnits
                .Where(pu => pu.ProductId == product.Id && pu.IsBaseUnit)
                .Select(pu => pu.UnitId)
                .FirstOrDefault();
            if (unitId == 0) unitId = product.DefaultUnitId;

            // Mark defective serial as Replaced
            defectiveSerial.CurrentStatus = "Replaced";

            // Create and post replacement StockOut
            var stockOut = new StockOut
            {
                DocumentCode = $"WRO-{DateTime.Now:yyyyMMddHHmmss}",
                CustomerId = customerId,
                WarehouseId = warehouseId,
                PurposeCode = "WarrantyReplacement",
                Status = DocumentStatus.Posted,
                ExportDate = DateTime.Now,
                Notes = $"Đổi serial BH cho claim #{claim.ClaimCode}",
                CreatedBy = userId,
                CreatedAt = DateTime.Now,
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

            var postingService = new InventoryPostingService(
                new EfInventoryUnitOfWork(db),
                new DbDefaultWarehouseProvider(db),
                new SystemClock());

            postingService.PostStockOut(new PostStockOutCommand(
                stockOut.Id,
                warehouseId,
                StockOutKind.WarrantyReplacement,
                StockDocumentStatus.Posted,
                product.Id,
                1,
                new[] { replacementSerial },
                userId));

            // Update Warranty Coverage
            var oldCoverage = claim.WarrantyCoverage;
            if (oldCoverage != null && oldCoverage.CoverageStatus == "Active")
            {
                oldCoverage.CoverageStatus = "Inactive";

                var remainingDays = (oldCoverage.WarrantyEndDate - DateTime.Now).TotalDays;
                if (remainingDays > 0)
                {
                    var newCoverage = new WarrantyCoverage
                    {
                        ProductSerialId = newSerial.Id,
                        CustomerId = oldCoverage.CustomerId,
                        SalesInvoiceId = oldCoverage.SalesInvoiceId,
                        WarrantyStartDate = DateTime.Now,
                        WarrantyEndDate = DateTime.Now.AddDays(remainingDays),
                        CoverageStatus = "Active"
                    };
                    db.WarrantyCoverages.Add(newCoverage);
                }
            }

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

            // Kiểm tra xem đã tạo chứng từ liên quan hay chưa
            bool hasRelatedStockIn = db.StockIns.Any(si => si.Notes.Contains(claim.ClaimCode));
            bool hasRelatedStockOut = db.StockOuts.Any(so => so.Notes.Contains(claim.ClaimCode)) || claim.ReplacementStockOutId.HasValue;

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
                .FirstOrDefault(c => c.ProductSerialId == serial.Id && c.CoverageStatus == "Active" && c.WarrantyEndDate >= DateTime.Now);
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
