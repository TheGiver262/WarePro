using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public partial class WarrantyClaimService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly DatabaseWriteExecutor _writeExecutor;

        public WarrantyClaimService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _writeExecutor = new DatabaseWriteExecutor(_contextFactory);
        }

        // so theo Date vì bảo hành tính theo ngày, không để phần giờ làm một ngày hợp lệ thành sai
        public static void EnsureValidCoverageDates(DateTime startDate, DateTime endDate)
        {
            if (endDate.Date < startDate.Date)
            {
                throw new InventoryDomainException(
                    "Warranty end date cannot be before warranty start date.");
            }
        }

        // Expired được suy ra theo ngày xem báo cáo; storedStatus vẫn giữ Active để không cần job cập nhật mỗi đêm
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

        // chỉnh sửa thường chỉ được đổi mô tả và ngày dự kiến; đổi status bắt buộc đi qua action hợp lệ
        private static void EnsureReplacementNotApplied(WarrantyClaim claim)
        {
            if (claim.ReplacementSerialId.HasValue || claim.ReplacementStockOutId.HasValue)
            {
                throw new InventoryDomainException(
                    $"Warranty claim {claim.Id} already has a replacement.");
            }
        }

        private static int GetDefaultWarehouseId(AppDbContext db)
        {
            return new DbDefaultWarehouseProvider(db).GetDefaultWarehouseId();
        }

        // posting dùng đơn vị cơ sở; thiếu cấu hình mới lùi về DefaultUnitId của sản phẩm
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

        // coverage cũ chuyển Inactive; serial mới chỉ nhận khoảng thời gian còn lại, không được gia hạn từ đầu
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
            var today = DateTime.Today;
            if (oldCoverage.WarrantyEndDate.Date < today)
            {
                return;
            }

            db.WarrantyCoverages.Add(new WarrantyCoverage
            {
                ProductSerialId = newSerialId,
                CustomerId = oldCoverage.CustomerId,
                SalesInvoiceId = oldCoverage.SalesInvoiceId,
                WarrantyStartDate = today,
                WarrantyEndDate = oldCoverage.WarrantyEndDate.Date,
                CoverageStatus = "Active"
            });
        }

        // bỏ claim đang đóng khỏi truy vấn; claim ManufacturerWait khác có ưu tiên trạng thái ReturnedToManufacturer
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

        // lookup chỉ trả coverage active và còn nằm trong khoảng ngày hiệu lực
        public WarrantyCoverage? GetCoverageBySerial(string serialNumber)
        {
            var normalizedSerial = QuanLyHangHoa.Helpers.SerialNumberNormalizer.Normalize(serialNumber);
            if (string.IsNullOrEmpty(normalizedSerial)) return null;

            using var db = _contextFactory();
            var serial = db.ProductSerials
                .FirstOrDefault(s => s.SerialNumber == normalizedSerial);

            if (serial == null) return null;

            var today = DateTime.Today;
            return db.WarrantyCoverages
                .FirstOrDefault(c =>
                    c.ProductSerialId == serial.Id
                    && c.CoverageStatus == "Active"
                    && c.WarrantyStartDate.Date <= today
                    && c.WarrantyEndDate.Date >= today);
        }

        // adapter giữ mọi thay đổi tồn kho đi qua InventoryPostingService thay vì sửa số dư trực tiếp
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
