using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services;

public sealed class AuditLogService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly DatabaseWriteExecutor _writeExecutor;

    public AuditLogService(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _writeExecutor = new DatabaseWriteExecutor(contextFactory);
    }

    public IReadOnlyList<AuditLog> GetAllAuditLogs(
        string? entityName = null,
        string? actionCode = null,
        int? performedBy = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? searchTerm = null)
    {
        using var db = _contextFactory();
        var query = db.AuditLogs
            .AsNoTracking()
            .Include(log => log.Performer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(entityName))
        {
            query = query.Where(log => log.EntityName == entityName);
        }

        if (!string.IsNullOrEmpty(actionCode))
        {
            query = query.Where(log => log.ActionCode == actionCode);
        }

        if (performedBy.HasValue)
        {
            query = query.Where(log => log.PerformedBy == performedBy.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(log => log.PerformedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(log => log.PerformedAt <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(log =>
                log.EntityId.ToString().Contains(searchTerm) ||
                log.ActionCode.Contains(searchTerm) ||
                log.EntityName.Contains(searchTerm));
        }

        return query
            .OrderByDescending(log => log.PerformedAt)
            .Take(1000)
            .ToList();
    }

    public IReadOnlyList<AuditLog> GetOldLogs(int years = 1) =>
        GetLogsBefore(DateTime.Now.AddYears(-years));

    public IReadOnlyList<AuditLog> GetLogsBefore(DateTime cutoffDate)
    {
        using var db = _contextFactory();
        return db.AuditLogs
            .AsNoTracking()
            .Include(log => log.Performer)
            .Where(log => log.PerformedAt < cutoffDate)
            .OrderBy(log => log.PerformedAt)
            .ToList();
    }

    public IReadOnlyList<AuditLog> GetLogsBetween(DateTime start, DateTime end)
    {
        using var db = _contextFactory();
        return db.AuditLogs
            .AsNoTracking()
            .Include(log => log.Performer)
            .Where(log => log.PerformedAt >= start && log.PerformedAt <= end)
            .OrderBy(log => log.PerformedAt)
            .ToList();
    }

    public async Task<AuditArchiveManifest> ArchiveLogsAsync(
        DateTime start,
        DateTime end,
        int actorId,
        string filePath,
        Action<IReadOnlyList<AuditLog>, string> export,
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (end < start)
        {
            throw new ArgumentException("Archive end must be on or after archive start.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(export);
        cancellationToken.ThrowIfCancellationRequested();

        var finalPath = Path.GetFullPath(filePath);
        // file tạm gắn với operation id để lần gọi lại tìm đúng file phục hồi nếu manifest đã commit; file chưa được công bố trước lúc đó
        var recoveryPath = BuildRecoveryPath(finalPath, operationId);
        var existing = await FindManifestAsync(operationId, cancellationToken);
        if (existing is not null)
        {
            ValidateReplay(existing, start, end, actorId, finalPath);
            FinalizeArchiveFile(existing, recoveryPath, finalPath);
            return existing;
        }

        // sắp xếp ổn định theo thời gian rồi id để lần đọc trong transaction có thể phát hiện tập log trong khoảng đã thay đổi
        IReadOnlyList<AuditLog> snapshot;
        await using (var readContext = _contextFactory())
        {
            snapshot = await readContext.AuditLogs
                .AsNoTracking()
                .Include(log => log.Performer)
                .Where(log => log.PerformedAt >= start && log.PerformedAt <= end)
                .OrderBy(log => log.PerformedAt)
                .ThenBy(log => log.Id)
                .ToListAsync(cancellationToken);
        }

        if (snapshot.Count == 0)
        {
            throw new InvalidOperationException(
                "No audit logs exist in the selected range.");
        }

        var directory = Path.GetDirectoryName(recoveryPath)
            ?? throw new InvalidOperationException("Archive directory is invalid.");
        Directory.CreateDirectory(directory);
        File.Delete(recoveryPath);
        // xuất file nằm ngoài transaction vì thao tác đĩa có thể lâu; transaction phía dưới sẽ kiểm tra lại snapshot trước khi ghi manifest
        export(snapshot, recoveryPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(recoveryPath))
        {
            throw new InvalidOperationException("Archive file was not created.");
        }

        var hash = ComputeHash(recoveryPath);
        var snapshotIds = snapshot.Select(log => log.Id).ToArray();
        var rangeStartUtc = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
        var rangeEndUtc = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();

        AuditArchiveManifest manifest;
        try
        {
            manifest = await _writeExecutor.ExecuteAsync(
                new DatabaseWriteRequest(
                    "audit.archive",
                    operationId,
                    IsolationLevel.Serializable),
                async (db, token) =>
                {
                    AuthorizationService.RequireFreshActor(
                        db,
                        actorId,
                        PermissionAction.ManageAuditLogs);

                    var replay = await db.AuditArchiveManifests
                        .SingleOrDefaultAsync(item => item.OperationId == operationId, token);
                    if (replay is not null)
                    {
                        return replay;
                    }

                    // đọc lại trong mức Serializable để manifest chỉ mô tả đúng tập log đã được ghi vào file tạm
                    var currentLogs = await db.AuditLogs
                        .Where(log => log.PerformedAt >= start && log.PerformedAt <= end)
                        .OrderBy(log => log.PerformedAt)
                        .ThenBy(log => log.Id)
                        .ToListAsync(token);
                    var currentIds = currentLogs.Select(log => log.Id).ToArray();
                    if (!snapshotIds.SequenceEqual(currentIds))
                    {
                        throw new InvalidOperationException(
                            "Audit logs changed while the archive file was being prepared. Please retry.");
                    }

                    var created = new AuditArchiveManifest
                    {
                        OperationId = operationId,
                        ActorId = actorId,
                        RangeStartUtc = rangeStartUtc,
                        RangeEndUtc = rangeEndUtc,
                        RowCount = currentLogs.Count,
                        FileName = Path.GetFileName(finalPath),
                        Sha256Hash = hash,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    db.AuditArchiveManifests.Add(created);
                    return created;
                },
                async (db, token) => await db.AuditArchiveManifests
                    .AnyAsync(item => item.OperationId == operationId, token),
                entityKey: operationId.ToString("N"),
                cancellationToken);
        }
        catch
        {
            // lỗi sau commit có thể làm client tưởng thất bại; chỉ xóa file phục hồi khi DB xác nhận chưa có manifest
            if (!await ManifestExistsAsync(operationId))
            {
                File.Delete(recoveryPath);
            }

            throw;
        }

        FinalizeArchiveFile(manifest, recoveryPath, finalPath);
        return manifest;
    }

    private async Task<AuditArchiveManifest?> FindManifestAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using var db = _contextFactory();
        return await db.AuditArchiveManifests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                manifest => manifest.OperationId == operationId,
                cancellationToken);
    }

    private async Task<bool> ManifestExistsAsync(Guid operationId)
    {
        await using var db = _contextFactory();
        return await db.AuditArchiveManifests
            .AsNoTracking()
            .AnyAsync(manifest => manifest.OperationId == operationId);
    }

    private static void ValidateReplay(
        AuditArchiveManifest manifest,
        DateTime start,
        DateTime end,
        int actorId,
        string finalPath)
    {
        var rangeStartUtc = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
        var rangeEndUtc = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime();
        if (manifest.ActorId != actorId ||
            manifest.RangeStartUtc != rangeStartUtc ||
            manifest.RangeEndUtc != rangeEndUtc ||
            !string.Equals(
                manifest.FileName,
                Path.GetFileName(finalPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The archive operation ID was already used with different parameters.");
        }
    }

    private static void FinalizeArchiveFile(
        AuditArchiveManifest manifest,
        string recoveryPath,
        string finalPath)
    {
        if (File.Exists(finalPath))
        {
            if (!string.Equals(
                    ComputeHash(finalPath),
                    manifest.Sha256Hash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "The existing archive file does not match its database manifest.");
            }

            File.Delete(recoveryPath);
            return;
        }

        if (!File.Exists(recoveryPath))
        {
            throw new FileNotFoundException(
                "The archive was committed but its recovery file is missing.",
                recoveryPath);
        }

        if (!string.Equals(
                ComputeHash(recoveryPath),
                manifest.Sha256Hash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The archive recovery file does not match its database manifest.");
        }

        File.Move(recoveryPath, finalPath, overwrite: true);
    }

    private static string BuildRecoveryPath(string finalPath, Guid operationId)
    {
        var directory = Path.GetDirectoryName(finalPath)
            ?? throw new InvalidOperationException("Archive directory is invalid.");
        var fileName = Path.GetFileNameWithoutExtension(finalPath);
        var extension = Path.GetExtension(finalPath);
        return Path.Combine(
            directory,
            $".{fileName}.{operationId:N}.archive{extension}");
    }

    private static string ComputeHash(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
