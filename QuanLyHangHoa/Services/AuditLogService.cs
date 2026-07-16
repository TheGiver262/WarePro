

using System;

using System.Collections.Generic;

using System.Linq;
using System.IO;
using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

using QuanLyHangHoa.Data;

using QuanLyHangHoa.Models;



namespace QuanLyHangHoa.Services;



public sealed class AuditLogService

{

    private readonly Func<AppDbContext> _contextFactory;
    


    public AuditLogService(Func<AppDbContext> contextFactory)
    
    {
    
        _contextFactory = contextFactory;
        
    }
    


    // query chỉ đọc, có performer để hiển thị và giới hạn 1000 dòng mới nhất tránh tải toàn bộ lịch sử
    public IReadOnlyList<AuditLog> GetAllAuditLogs(
    
        string? entityName = null,
        
        string? actionCode = null,
        
        int? performedBy = null,
        
        DateTime? fromDate = null,
        
        DateTime? toDate = null,
        
        string? searchTerm = null)
        
    {
    
        using var db = _contextFactory();
        
        var query = db.AuditLogs.AsNoTracking()
        
            .Include(a => a.Performer)
            
            .AsQueryable();
            


        if (!string.IsNullOrEmpty(entityName)) query = query.Where(a => a.EntityName == entityName);
        
        if (!string.IsNullOrEmpty(actionCode)) query = query.Where(a => a.ActionCode == actionCode);
        
        if (performedBy.HasValue) query = query.Where(a => a.PerformedBy == performedBy.Value);
        
        if (fromDate.HasValue) query = query.Where(a => a.PerformedAt >= fromDate.Value);
        
        if (toDate.HasValue) query = query.Where(a => a.PerformedAt <= toDate.Value);
        


        if (!string.IsNullOrEmpty(searchTerm))
        
        {
        
            query = query.Where(a =>
            
                a.EntityId.ToString().Contains(searchTerm) ||
                
                a.ActionCode.Contains(searchTerm) ||
                
                a.EntityName.Contains(searchTerm));
                
        }
        


        return query
        
            .OrderByDescending(a => a.PerformedAt)
            
            .Take(1000)
            
            .ToList();
            
    }
    


    // cutoff tách chính sách lưu trữ khỏi giao diện; log được sắp cũ đến mới để xuất archive
    public IReadOnlyList<AuditLog> GetOldLogs(int years = 1)
    
    {
    
        using var db = _contextFactory();
        
        var cutoffDate = DateTime.Now.AddYears(-years);
        
        return db.AuditLogs.AsNoTracking()
        
            .Include(a => a.Performer)
            
            .Where(a => a.PerformedAt < cutoffDate)
            
            .OrderBy(a => a.PerformedAt)
            
            .ToList();
            
    }
    


    public IReadOnlyList<AuditLog> GetLogsBefore(DateTime cutoffDate)
    
    {
    
        using var db = _contextFactory();
        
        return db.AuditLogs.AsNoTracking()
        
            .Include(a => a.Performer)
            
            .Where(a => a.PerformedAt < cutoffDate)
            
            .OrderBy(a => a.PerformedAt)
            
            .ToList();
            
    }
    


    public IReadOnlyList<AuditLog> GetLogsBetween(DateTime start, DateTime end)
    
    {
    
        using var db = _contextFactory();
        
        return db.AuditLogs.AsNoTracking()
        
            .Include(a => a.Performer)
            
            .Where(a => a.PerformedAt >= start && a.PerformedAt <= end)
            
            .OrderBy(a => a.PerformedAt)
            
            .ToList();
            
    }
    


    // xóa trực tiếp chỉ dùng cho tập id đã được lớp gọi xác nhận; nghiệp vụ archive nên dùng ArchiveLogs
    public int DeleteLogs(IEnumerable<int> logIds)
    
    {
    
        using var db = _contextFactory();
        
        var logsToRemove = db.AuditLogs.Where(a => logIds.Contains(a.Id));
        
        var count = logsToRemove.Count();
        
        db.AuditLogs.RemoveRange(logsToRemove);
        
        db.SaveChanges();
        
        return count;
        
    }
    

    // quy trình archive: xuất file trước, xác nhận file tồn tại, tính SHA-256 rồi mới xóa log trong transaction
    public AuditArchiveManifest ArchiveLogs(
        DateTime start,
        DateTime end,
        int actorId,
        string filePath,
        Action<IReadOnlyList<AuditLog>, string> export)
    {
        if (end < start)
            throw new ArgumentException("Archive end must be on or after archive start.");
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(export);

        using var db = _contextFactory();
        var logs = db.AuditLogs
            .Where(log => log.PerformedAt >= start && log.PerformedAt <= end)
            .OrderBy(log => log.PerformedAt)
            .ToList();
        if (logs.Count == 0)
            throw new InvalidOperationException("No audit logs exist in the selected range.");

        export(logs, filePath);
        if (!File.Exists(filePath))
            throw new InvalidOperationException("Archive file was not created.");

        // hash cho phép kiểm tra file archive có bị thay đổi sau khi rời database hay không
        string hash;
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            hash = Convert.ToHexString(SHA256.HashData(stream));

        // manifest và việc xóa log phải cùng commit; không để database mất log mà thiếu dấu vết file archive
        using var transaction = db.Database.BeginTransaction();
        var manifest = new AuditArchiveManifest
        {
            ActorId = actorId,
            RangeStartUtc = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime(),
            RangeEndUtc = end.Kind == DateTimeKind.Utc ? end : end.ToUniversalTime(),
            RowCount = logs.Count,
            FileName = Path.GetFileName(filePath),
            Sha256Hash = hash,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AuditArchiveManifests.Add(manifest);
        db.AuditLogs.RemoveRange(logs);
        db.SaveChanges();
        transaction.Commit();
        return manifest;
    }
}

