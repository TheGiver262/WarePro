using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public partial class WarrantyClaimService
{
    public Task UpdateClaimAsync(
        int claimId,
        string? problemDescription,
        DateTime? expectedReturnDate,
        byte[] expectedRowVersion,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.update",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (_, claim) =>
            {
                WarrantyClaimTransitions.EnsureMutable(claim.Status);
                claim.ProblemDescription = problemDescription?.Trim();
                claim.ExpectedReturnDate = expectedReturnDate;
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.ProblemDescription == problemDescription
                && item.ExpectedReturnDate == expectedReturnDate,
                token),
            cancellationToken);

    public Task ResolveClaimAsync(
        int claimId,
        string resolutionType,
        string technicalConclusion,
        int approverId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.resolve",
            claimId,
            expectedRowVersion,
            approverId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (_, claim) =>
            {
                WarrantyClaimTransitions.EnsureAllowed(claim, WarrantyClaimAction.Resolve);
                claim.ResolutionType = resolutionType.Trim();
                claim.TechnicalConclusion = technicalConclusion.Trim();
                claim.ApprovedBy = approverId;
                claim.Status = "Ready";
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "Ready"
                && item.ResolutionType == resolutionType
                && item.TechnicalConclusion == technicalConclusion
                && item.ApprovedBy == approverId,
                token),
            cancellationToken);

    public Task CloseClaimAsync(
        int claimId,
        string note,
        int userId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.close",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (db, claim) =>
            {
                WarrantyClaimTransitions.EnsureAllowed(claim, WarrantyClaimAction.Close);
                claim.ProcessingNote = note.Trim();
                claim.Status = "Closed";
                claim.ClosedDate = DateTime.Now;
                UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId && item.Status == "Closed" && item.ProcessingNote == note,
                token),
            cancellationToken);

    public async Task<int> CreateClaimAsync(
        string claimCode,
        string serialNumber,
        string problemDescription,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        // chuẩn hóa input trước executor để mọi attempt dùng cùng mã claim, serial và mô tả.
        var normalizedCode = claimCode.Trim();
        var normalizedSerial = serialNumber.Trim();
        var normalizedProblem = problemDescription.Trim();

        try
        {
            return await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "warranty.claim.create",
                    operationId,
                    System.Data.IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        userId,
                        PermissionAction.CreateWarrantyClaim);

                    var serial = await db.ProductSerials.SingleOrDefaultAsync(
                        item => item.SerialNumber == normalizedSerial,
                        token)
                        ?? throw new InvalidOperationException(
                            $"Serial {normalizedSerial} không tồn tại.");

                    var existing = await db.WarrantyClaims.AsNoTracking()
                        .SingleOrDefaultAsync(item => item.ClaimCode == normalizedCode, token);
                    if (existing is not null)
                    {
                        if (existing.ProductSerialId == serial.Id
                            && existing.ProblemDescription == normalizedProblem
                            && existing.ProcessedBy == userId)
                        {
                            return existing.Id;
                        }

                        throw new InvalidOperationException(
                            $"Mã phiếu bảo hành {normalizedCode} đã tồn tại.");
                    }

                    var hasOpenClaim = await db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                        item.ProductSerialId == serial.Id
                        && item.Status != "Closed"
                        && item.Status != "Rejected",
                        token);
                    if (hasOpenClaim)
                    {
                        throw OpenClaimExists(normalizedSerial);
                    }

                    // kiểm tra coverage còn hiệu lực trong transaction ghi, không tin kết quả lookup đã đọc trước đó ở UI.
                    var today = DateTime.Today;
                    var coverage = await db.WarrantyCoverages.SingleOrDefaultAsync(item =>
                        item.ProductSerialId == serial.Id
                        && item.CoverageStatus == "Active"
                        && item.WarrantyStartDate.Date <= today
                        && item.WarrantyEndDate.Date >= today,
                        token)
                        ?? throw new InvalidOperationException(
                            $"Serial {normalizedSerial} không có bảo hành còn hiệu lực.");

                    if (serial.CurrentStatus is not "InWarrantyProcess" and not "ReturnedToManufacturer")
                    {
                        serial.CurrentStatus = "InWarrantyProcess";
                    }

                    var claim = new WarrantyClaim
                    {
                        ClaimCode = normalizedCode,
                        ProductSerialId = serial.Id,
                        WarrantyCoverageId = coverage.Id,
                        ProblemDescription = normalizedProblem,
                        ReceivedDate = DateTime.Now,
                        Status = "Open",
                        ProcessedBy = userId
                    };
                    // flush trong transaction để claim nhận id; executor chỉ commit sau khi toàn bộ callback thành công.
                    db.WarrantyClaims.Add(claim);
                    await db.SaveChangesAsync(token);
                    return claim.Id;
                },
                (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                    item.ClaimCode == normalizedCode
                    && item.ProcessedBy == userId
                    && item.ProblemDescription == normalizedProblem,
                    token),
                entityKey: normalizedCode,
                cancellationToken: cancellationToken);
        }
        catch (DbUpdateException ex) when (IsOpenClaimUniqueViolation(ex))
        {
            throw OpenClaimExists(normalizedSerial, ex);
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                "Không thể tạo phiếu bảo hành. Vui lòng kiểm tra mã phiếu và dữ liệu bảo hành đã tồn tại.",
                ex);
        }
    }

    private static InvalidOperationException OpenClaimExists(
        string serialNumber,
        Exception? innerException = null)
    {
        var message = $"Serial {serialNumber} đang có phiếu bảo hành chưa kết thúc.";
        return innerException is null
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, innerException);
    }

    private static bool IsOpenClaimUniqueViolation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("UX_WarrantyClaim_OpenProductSerialId", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("WarrantyClaim.OpenProductSerialId", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("WarrantyClaim.ProductSerialId", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public Task CompleteRepairAsync(
        int claimId,
        string technicalConclusion,
        int userId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.complete-repair",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (db, claim) =>
            {
                WarrantyClaimTransitions.EnsureAllowed(
                    claim,
                    WarrantyClaimAction.CompleteShopRepair);
                claim.TechnicalConclusion = technicalConclusion.Trim();
                claim.Status = "Ready";
                claim.ResolutionType = "Repair";
                claim.ApprovedBy = userId;
                UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "Ready"
                && item.ResolutionType == "Repair"
                && item.TechnicalConclusion == technicalConclusion
                && item.ApprovedBy == userId,
                token),
            cancellationToken);

    public Task SendToManufacturerAsync(
        int claimId,
        string? manufacturerName,
        string? trackingCode,
        DateTime? expectedReturnDate,
        string note,
        int userId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.send-manufacturer",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (db, claim) =>
            {
                WarrantyClaimTransitions.EnsureAllowed(claim, WarrantyClaimAction.Send);
                claim.ManufacturerName = manufacturerName?.Trim();
                claim.ManufacturerTrackingCode = trackingCode?.Trim();
                claim.ManufacturerExpectedReturnDate = expectedReturnDate;
                claim.ManufacturerResult = note.Trim();
                claim.Status = "ManufacturerWait";
                claim.ProcessedBy = userId;
                var serial = db.ProductSerials.SingleOrDefault(
                    item => item.Id == claim.ProductSerialId);
                if (serial is not null)
                {
                    serial.CurrentStatus = "ReturnedToManufacturer";
                }
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "ManufacturerWait"
                && item.ManufacturerName == manufacturerName
                && item.ManufacturerTrackingCode == trackingCode
                && item.ManufacturerExpectedReturnDate == expectedReturnDate
                && item.ManufacturerResult == note,
                token),
            cancellationToken);

    public Task ReceiveFromManufacturerRepairedAsync(
        int claimId,
        string conclusion,
        int userId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.receive-repaired",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (db, claim) =>
            {
                WarrantyClaimTransitions.EnsureAllowed(
                    claim,
                    WarrantyClaimAction.ReceiveManufacturerRepair);
                claim.TechnicalConclusion = conclusion.Trim();
                claim.ResolutionType = "ManufacturerRepair";
                claim.Status = "Closed";
                claim.ClosedDate = DateTime.Now;
                claim.ApprovedBy = userId;
                UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "Closed"
                && item.ResolutionType == "ManufacturerRepair"
                && item.TechnicalConclusion == conclusion
                && item.ApprovedBy == userId,
                token),
            cancellationToken);

    public Task ReceiveFromManufacturerReplacedAsync(
        int claimId,
        string newSerialNumber,
        string conclusion,
        int userId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var rowVersion = expectedRowVersion.ToArray();
        var normalizedSerial = newSerialNumber.Trim();
        var stockInCode = $"WRI-{claimId}-{operationId.ToString("N")[..12]}";
        var stockOutCode = $"WRO-{claimId}-{operationId.ToString("N")[..12]}";

        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                "warranty.claim.receive-replaced",
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    userId,
                    PermissionAction.CreateWarrantyClaim);
                var claim = await db.WarrantyClaims
                    .Include(item => item.ProductSerial)
                        .ThenInclude(serial => serial.Product)
                    .Include(item => item.WarrantyCoverage)
                    .SingleOrDefaultAsync(item => item.Id == claimId, token)
                    ?? throw new InvalidOperationException(
                        $"Phiếu bảo hành #{claimId} không tồn tại.");
                db.Entry(claim).Property(item => item.RowVersion).OriginalValue = rowVersion;
                WarrantyClaimTransitions.EnsureAllowed(
                    claim,
                    WarrantyClaimAction.ReceiveManufacturerReplacement);
                EnsureReplacementNotApplied(claim);

                var defectiveSerial = claim.ProductSerial;
                var product = defectiveSerial.Product;
                var warehouseId = GetDefaultWarehouseId(db);
                var unitId = GetBaseUnitId(db, product);
                var now = DateTime.Now;
                defectiveSerial.CurrentStatus = "Replaced";

                var stockIn = new StockIn
                {
                    DocumentCode = stockInCode,
                    WarehouseId = warehouseId,
                    PurposeCode = "WarrantyReceive",
                    Status = StockDocumentStatus.Approved.ToString(),
                    ImportDate = now,
                    Notes = $"Nhận serial mới từ hãng BH cho claim #{claim.ClaimCode}",
                    CreatedBy = userId,
                    CreatedAt = now,
                    ApprovedBy = userId,
                    ApprovedAt = now,
                    PostedBy = userId,
                    PostedAt = now,
                    Lines =
                    [
                        new StockInLine
                        {
                            ProductId = product.Id,
                            UnitId = unitId,
                            Quantity = 1,
                            BaseQuantity = 1,
                            UnitPrice = 0
                        }
                    ]
                };
                db.StockIns.Add(stockIn);
                await db.SaveChangesAsync(token);

                var postingService = CreatePostingService(db);
                postingService.PostStockIn(new PostStockInCommand(
                    stockIn.Id,
                    warehouseId,
                    StockInKind.WarrantyReceive,
                    StockDocumentStatus.Approved,
                    product.Id,
                    1,
                    [normalizedSerial],
                    userId));

                var newSerial = await db.ProductSerials.SingleOrDefaultAsync(
                    item => item.SerialNumber == normalizedSerial,
                    token)
                    ?? throw new InvalidOperationException(
                        $"Serial mới {normalizedSerial} không tìm thấy sau khi nhập kho.");

                var stockOut = CreateReplacementStockOut(
                    stockOutCode,
                    claim,
                    product,
                    newSerial,
                    claim.WarrantyCoverage.CustomerId,
                    warehouseId,
                    unitId,
                    userId,
                    now);
                db.StockOuts.Add(stockOut);
                await db.SaveChangesAsync(token);

                postingService.PostStockOut(new PostStockOutCommand(
                    stockOut.Id,
                    warehouseId,
                    StockOutKind.WarrantyReplacement,
                    StockDocumentStatus.Approved,
                    product.Id,
                    1,
                    [normalizedSerial],
                    userId));

                TransferRemainingCoverage(db, claim.WarrantyCoverage, newSerial.Id);
                claim.TechnicalConclusion = conclusion.Trim();
                claim.ResolutionType = "ManufacturerReplace";
                claim.ReplacementSerialId = newSerial.Id;
                claim.ReplacementStockOutId = stockOut.Id;
                claim.Status = "Closed";
                claim.ClosedDate = now;
                claim.ApprovedBy = userId;
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "Closed"
                && item.ResolutionType == "ManufacturerReplace"
                && item.ReplacementSerial != null
                && item.ReplacementSerial.SerialNumber == normalizedSerial
                && item.ReplacementStockOut != null
                && item.ReplacementStockOut.DocumentCode == stockOutCode,
                token),
            entityKey: claimId.ToString(),
            cancellationToken: cancellationToken);
    }

    public Task RejectClaimAsync(
        int claimId,
        string reason,
        int userId,
        byte[] expectedRowVersion,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.reject",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (db, claim) =>
            {
                WarrantyClaimTransitions.EnsureAllowed(claim, WarrantyClaimAction.Reject);
                claim.RejectionReason = reason.Trim();
                claim.Status = "Rejected";
                claim.ResolutionType = "Reject";
                claim.ApprovedBy = userId;
                claim.ClosedDate = DateTime.Now;
                UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "Rejected"
                && item.RejectionReason == reason
                && item.ApprovedBy == userId,
                token),
            cancellationToken);

    public Task ReplaceSerialAsync(
        int claimId,
        string replacementSerial,
        string conclusion,
        byte[] expectedRowVersion,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var rowVersion = expectedRowVersion.ToArray();
        // operation id đi vào mã chứng từ để verifier nhận ra đúng lần đổi serial khi phản hồi commit bị mất.
        var normalizedSerial = replacementSerial.Trim();
        var stockOutCode = $"WRO-{claimId}-{operationId.ToString("N")[..12]}";

        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                "warranty.claim.replace-stock",
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    userId,
                    PermissionAction.CreateWarrantyClaim);
                var claim = await db.WarrantyClaims
                    .Include(item => item.ProductSerial)
                        .ThenInclude(serial => serial.Product)
                    .Include(item => item.WarrantyCoverage)
                    .SingleOrDefaultAsync(item => item.Id == claimId, token)
                    ?? throw new InvalidOperationException(
                        $"Phiếu bảo hành #{claimId} không tồn tại.");
                db.Entry(claim).Property(item => item.RowVersion).OriginalValue = rowVersion;
                WarrantyClaimTransitions.EnsureAllowed(
                    claim,
                    WarrantyClaimAction.ReplaceFromStock);
                EnsureReplacementNotApplied(claim);

                var defectiveSerial = claim.ProductSerial;
                var product = defectiveSerial.Product;
                var warehouseId = GetDefaultWarehouseId(db);
                var newSerial = await db.ProductSerials.SingleOrDefaultAsync(item =>
                    item.SerialNumber == normalizedSerial
                    && item.CurrentStatus == "InStock"
                    && item.CurrentWarehouseId == warehouseId,
                    token)
                    ?? throw new InvalidOperationException(
                        $"Serial {normalizedSerial} không có trong kho hoặc không ở trạng thái sẵn sàng.");

                if (newSerial.ProductId != product.Id)
                {
                    throw new InvalidOperationException(
                        $"Serial {normalizedSerial} không thuộc sản phẩm {product.DisplayName}.");
                }

                var unitId = GetBaseUnitId(db, product);
                var now = DateTime.Now;
                defectiveSerial.CurrentStatus = "Replaced";
                var stockOut = CreateReplacementStockOut(
                    stockOutCode,
                    claim,
                    product,
                    newSerial,
                    claim.WarrantyCoverage.CustomerId,
                    warehouseId,
                    unitId,
                    userId,
                    now);
                db.StockOuts.Add(stockOut);
                await db.SaveChangesAsync(token);

                CreatePostingService(db).PostStockOut(new PostStockOutCommand(
                    stockOut.Id,
                    warehouseId,
                    StockOutKind.WarrantyReplacement,
                    StockDocumentStatus.Approved,
                    product.Id,
                    1,
                    [normalizedSerial],
                    userId));

                TransferRemainingCoverage(db, claim.WarrantyCoverage, newSerial.Id);
                claim.ReplacementSerialId = newSerial.Id;
                claim.ReplacementStockOutId = stockOut.Id;
                claim.TechnicalConclusion = conclusion.Trim();
                claim.Status = "Closed";
                claim.ResolutionType = "Replace";
                claim.ApprovedBy = userId;
                claim.ClosedDate = now;
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AnyAsync(item =>
                item.Id == claimId
                && item.Status == "Closed"
                && item.ResolutionType == "Replace"
                && item.ReplacementSerial != null
                && item.ReplacementSerial.SerialNumber == normalizedSerial
                && item.ReplacementStockOut != null
                && item.ReplacementStockOut.DocumentCode == stockOutCode,
                token),
            entityKey: claimId.ToString(),
            cancellationToken: cancellationToken);
    }

    public Task DeleteClaimAsync(
        int claimId,
        byte[] expectedRowVersion,
        int userId,
        Guid operationId,
        CancellationToken cancellationToken = default) =>
        ExecuteClaimTransitionAsync(
            "warranty.claim.delete",
            claimId,
            expectedRowVersion,
            userId,
            operationId,
            (db, token) => db.WarrantyClaims.SingleOrDefaultAsync(
                item => item.Id == claimId,
                token),
            (db, claim) =>
            {
                WarrantyClaimTransitions.EnsureMutable(claim.Status);
                var hasRelatedStockIn = db.StockIns.Any(item =>
                    item.Notes != null && item.Notes.Contains(claim.ClaimCode));
                var hasRelatedStockOut = db.StockOuts.Any(item =>
                    item.Notes != null && item.Notes.Contains(claim.ClaimCode))
                    || claim.ReplacementStockOutId.HasValue;
                if (hasRelatedStockIn || hasRelatedStockOut)
                {
                    throw new InvalidOperationException(
                        "Không thể xóa phiếu bảo hành khi đã phát sinh chứng từ liên quan.");
                }

                UpdateSerialStatusOnClaimClosure(db, claim.ProductSerialId, claim.Id);
                db.WarrantyClaims.Remove(claim);
            },
            (db, token) => db.WarrantyClaims.AsNoTracking().AllAsync(
                item => item.Id != claimId,
                token),
            cancellationToken);

    // helper gom transition để permission, rowversion và uncertain-commit verification luôn đi cùng một transaction.
    private Task ExecuteClaimTransitionAsync(
        string operationName,
        int claimId,
        byte[] expectedRowVersion,
        int userId,
        Guid operationId,
        Func<AppDbContext, CancellationToken, Task<WarrantyClaim?>> loadClaim,
        Action<AppDbContext, WarrantyClaim> stage,
        Func<AppDbContext, CancellationToken, Task<bool>> verifySucceeded,
        CancellationToken cancellationToken)
    {
        var rowVersion = expectedRowVersion.ToArray();
        return _writeExecutor.ExecuteAsync(
            new DatabaseWriteRequest(
                operationName,
                operationId,
                System.Data.IsolationLevel.Serializable),
            async (db, token) =>
            {
                AuthorizationService.RequireFreshActor(
                    db,
                    userId,
                    PermissionAction.CreateWarrantyClaim);
                var claim = await loadClaim(db, token)
                    ?? throw new InvalidOperationException(
                        $"Phiếu bảo hành #{claimId} không tồn tại.");
                db.Entry(claim).Property(item => item.RowVersion).OriginalValue = rowVersion;
                stage(db, claim);
            },
            verifySucceeded,
            entityKey: claimId.ToString(),
            cancellationToken: cancellationToken);
    }

    private static StockOut CreateReplacementStockOut(
        string documentCode,
        WarrantyClaim claim,
        Product product,
        ProductSerial newSerial,
        int customerId,
        int warehouseId,
        int unitId,
        int userId,
        DateTime now) =>
        new()
        {
            DocumentCode = documentCode,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            PurposeCode = "WarrantyReplacement",
            Status = StockDocumentStatus.Approved.ToString(),
            ExportDate = now,
            Notes = $"Đổi serial BH cho claim #{claim.ClaimCode}",
            CreatedBy = userId,
            CreatedAt = now,
            ApprovedBy = userId,
            ApprovedAt = now,
            PostedBy = userId,
            PostedAt = now,
            Lines =
            [
                new StockOutLine
                {
                    ProductId = product.Id,
                    UnitId = unitId,
                    Quantity = 1,
                    BaseQuantity = 1,
                    UnitPrice = 0,
                    ProductSerials = [newSerial]
                }
            ]
        };

    internal int CreateClaim(
        string claimCode,
        string serialNumber,
        string problemDescription,
        int userId) =>
        CreateClaimAsync(
            claimCode,
            serialNumber,
            problemDescription,
            userId,
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void UpdateClaim(WarrantyClaim claim)
    {
        var userId = claim.ProcessedBy > 0 ? claim.ProcessedBy : 1;
        UpdateClaimAsync(
            claim.Id,
            claim.ProblemDescription,
            claim.ExpectedReturnDate,
            claim.RowVersion,
            userId,
            Guid.NewGuid()).GetAwaiter().GetResult();
    }

    internal void ResolveClaim(
        int claimId,
        string resolutionType,
        string technicalConclusion,
        int approverId) =>
        ResolveClaimAsync(
            claimId,
            resolutionType,
            technicalConclusion,
            approverId,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void CloseClaim(int claimId, string note) =>
        CloseClaimAsync(
            claimId,
            note,
            1,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void CompleteRepair(int claimId, string technicalConclusion, int userId) =>
        CompleteRepairAsync(
            claimId,
            technicalConclusion,
            userId,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void SendToManufacturer(
        int claimId,
        string manufacturerName,
        string trackingCode,
        DateTime? expectedReturnDate,
        string note,
        int userId) =>
        SendToManufacturerAsync(
            claimId,
            manufacturerName,
            trackingCode,
            expectedReturnDate,
            note,
            userId,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void SendToManufacturer(int claimId, string manufacturerNote, int userId) =>
        SendToManufacturer(
            claimId,
            null!,
            null!,
            null,
            manufacturerNote,
            userId);

    internal void ReceiveFromManufacturerRepaired(
        int claimId,
        string conclusion,
        int userId) =>
        ReceiveFromManufacturerRepairedAsync(
            claimId,
            conclusion,
            userId,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void ReceiveFromManufacturerReplaced(
        int claimId,
        string newSerialNumber,
        string conclusion,
        int userId) =>
        ReceiveFromManufacturerReplacedAsync(
            claimId,
            newSerialNumber,
            conclusion,
            userId,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void RejectClaim(int claimId, string reason, int userId) =>
        RejectClaimAsync(
            claimId,
            reason,
            userId,
            GetClaimRowVersion(claimId),
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void ReplaceSerial(
        int claimId,
        string replacementSerial,
        string conclusion,
        int userId) =>
        ReplaceSerialAsync(
            claimId,
            replacementSerial,
            conclusion,
            GetClaimRowVersion(claimId),
            userId,
            Guid.NewGuid()).GetAwaiter().GetResult();

    internal void DeleteClaim(int claimId) =>
        DeleteClaimAsync(
            claimId,
            GetClaimRowVersion(claimId),
            1,
            Guid.NewGuid()).GetAwaiter().GetResult();

    private byte[] GetClaimRowVersion(int claimId)
    {
        using var db = _contextFactory();
        return db.WarrantyClaims.AsNoTracking()
            .Where(item => item.Id == claimId)
            .Select(item => item.RowVersion)
            .Single();
    }
}
