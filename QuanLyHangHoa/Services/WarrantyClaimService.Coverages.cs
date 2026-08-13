using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;

namespace QuanLyHangHoa.Services;

public partial class WarrantyClaimService
{
    public Task UpdateCoverageAsync(
        int coverageId,
        DateTime startDate,
        DateTime endDate,
        string status,
        byte[] expectedRowVersion,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        // sao chép token trước callback retry; context mới vẫn so sánh đúng phiên bản người dùng đã đọc.
        EnsureValidCoverageDates(startDate, endDate);
        var rowVersion = expectedRowVersion.ToArray();
        var normalizedStatus = status.Trim();

        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                "warranty.coverage.update",
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    userId,
                    PermissionAction.CreateWarrantyClaim);
                var coverage = await db.WarrantyCoverages.SingleOrDefaultAsync(
                    item => item.Id == coverageId,
                    token)
                    ?? throw new InventoryDomainException(
                        $"Thông tin bảo hành #{coverageId} không tồn tại.");
                db.Entry(coverage).Property(item => item.RowVersion).OriginalValue = rowVersion;
                coverage.WarrantyStartDate = startDate;
                coverage.WarrantyEndDate = endDate;
                coverage.CoverageStatus = normalizedStatus;
            },
            (db, token) => db.WarrantyCoverages.AsNoTracking().AnyAsync(item =>
                item.Id == coverageId
                && item.WarrantyStartDate == startDate
                && item.WarrantyEndDate == endDate
                && item.CoverageStatus == normalizedStatus,
                token),
            entityKey: coverageId.ToString(),
            cancellationToken: cancellationToken);
    }

    public Task DeleteCoverageAsync(
        int coverageId,
        byte[] expectedRowVersion,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var rowVersion = expectedRowVersion.ToArray();
        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                "warranty.coverage.delete",
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    userId,
                    PermissionAction.CreateWarrantyClaim);
                var coverage = await db.WarrantyCoverages.SingleOrDefaultAsync(
                    item => item.Id == coverageId,
                    token)
                    ?? throw new InventoryDomainException(
                        $"Thông tin bảo hành #{coverageId} không tồn tại.");
                db.Entry(coverage).Property(item => item.RowVersion).OriginalValue = rowVersion;
                db.WarrantyCoverages.Remove(coverage);
            },
            (db, token) => db.WarrantyCoverages.AsNoTracking().AllAsync(
                item => item.Id != coverageId,
                token),
            entityKey: coverageId.ToString(),
            cancellationToken: cancellationToken);
    }
}
