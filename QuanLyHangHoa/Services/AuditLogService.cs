

using System;

using System.Collections.Generic;

using System.Linq;

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
    


    public int DeleteLogs(IEnumerable<int> logIds)
    
    {
    
        using var db = _contextFactory();
        
        var logsToRemove = db.AuditLogs.Where(a => logIds.Contains(a.Id));
        
        var count = logsToRemove.Count();
        
        db.AuditLogs.RemoveRange(logsToRemove);
        
        db.SaveChanges();
        
        return count;
        
    }
    
}

