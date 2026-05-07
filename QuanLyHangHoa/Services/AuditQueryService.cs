using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using Microsoft.EntityFrameworkCore;

namespace QuanLyHangHoa.Services
{
    public enum AuditTimelineEntryKind
    {
        Audit,
        StockLedger
    }

    public sealed record AuditTimelineEntry(
        AuditTimelineEntryKind Kind,
        int EntityId,
        DateTime OccurredAt,
        string Action,
        int UserId,
        int? ProductId,
        int? WarehouseId,
        decimal? Quantity);

    public class AuditQueryService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public AuditQueryService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public IReadOnlyList<AuditTimelineEntry> GetEntityTimeline(string entityName, int entityId)
        {
            using var db = _contextFactory();
            var auditEntries = db.AuditLogs.AsNoTracking()
                .Where(audit => audit.EntityName == entityName && audit.EntityId == entityId)
                .Select(audit => new AuditTimelineEntry(
                    AuditTimelineEntryKind.Audit,
                    audit.EntityId,
                    audit.PerformedAt,
                    audit.ActionCode,
                    audit.PerformedBy,
                    null,
                    null,
                    null));

            var ledgerEntries = db.StockLedgers.AsNoTracking()
                .Where(ledger => ledger.SourceDocumentType == entityName && ledger.SourceDocumentId == entityId)
                .Select(ledger => new AuditTimelineEntry(
                    AuditTimelineEntryKind.StockLedger,
                    ledger.SourceDocumentId,
                    ledger.PostedAt,
                    ledger.MovementType,
                    ledger.PostedBy,
                    ledger.ProductId,
                    ledger.WarehouseId,
                    ledger.Quantity));

            return auditEntries
                .AsEnumerable()
                .Concat(ledgerEntries.AsEnumerable())
                .OrderBy(entry => entry.OccurredAt)
                .ThenBy(entry => entry.Kind)
                .ToList();
        }

        public IReadOnlyList<AuditTimelineEntry> GetProductLedger(int productId)
        {
            using var db = _contextFactory();
            return db.StockLedgers.AsNoTracking()
                .Where(ledger => ledger.ProductId == productId)
                .OrderByDescending(ledger => ledger.PostedAt)
                .Select(ledger => new AuditTimelineEntry(
                    AuditTimelineEntryKind.StockLedger,
                    ledger.SourceDocumentId,
                    ledger.PostedAt,
                    ledger.MovementType,
                    ledger.PostedBy,
                    ledger.ProductId,
                    ledger.WarehouseId,
                    ledger.Quantity))
                .ToList();
        }
        public IReadOnlyList<AuditTimelineEntry> GetDocumentTimeline(Guid documentId)
        {
            // For now, documents are tracked via string IDs or integer IDs. 
            // If the UI passes a Guid, we might need to map it.
            // Assuming for now it maps to SourceDocumentId if available, or we search by string representation.
            
            using var db = _contextFactory();
            var docIdStr = documentId.ToString();

            var auditEntries = db.AuditLogs.AsNoTracking()
                .Where(audit => audit.EntityId.ToString() == docIdStr || audit.ActionCode.Contains(docIdStr))
                .Select(audit => new AuditTimelineEntry(
                    AuditTimelineEntryKind.Audit,
                    audit.EntityId,
                    audit.PerformedAt,
                    audit.ActionCode,
                    audit.PerformedBy,
                    null,
                    null,
                    null));

            var ledgerEntries = db.StockLedgers.AsNoTracking()
                .Where(ledger => ledger.SourceDocumentId.ToString() == docIdStr)
                .Select(ledger => new AuditTimelineEntry(
                    AuditTimelineEntryKind.StockLedger,
                    ledger.SourceDocumentId,
                    ledger.PostedAt,
                    ledger.MovementType,
                    ledger.PostedBy,
                    ledger.ProductId,
                    ledger.WarehouseId,
                    ledger.Quantity));

            return auditEntries
                .AsEnumerable()
                .Concat(ledgerEntries.AsEnumerable())
                .OrderBy(entry => entry.OccurredAt)
                .ToList();
        }

        public IReadOnlyList<QuanLyHangHoa.Models.AuditLog> GetAllAuditLogs(
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

            if (!string.IsNullOrEmpty(entityName))
                query = query.Where(a => a.EntityName == entityName);

            if (!string.IsNullOrEmpty(actionCode))
                query = query.Where(a => a.ActionCode == actionCode);

            if (performedBy.HasValue)
                query = query.Where(a => a.PerformedBy == performedBy.Value);

            if (fromDate.HasValue)
                query = query.Where(a => a.PerformedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(a => a.PerformedAt <= toDate.Value);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(a => a.EntityId.ToString().Contains(searchTerm) || 
                                         a.ActionCode.Contains(searchTerm) ||
                                         a.EntityName.Contains(searchTerm));
            }

            return query
                .OrderByDescending(a => a.PerformedAt)
                .Take(1000) // Limit for performance
                .ToList();
        }

        public IReadOnlyList<QuanLyHangHoa.Models.AuditLog> GetOldLogs(int years = 1)
        {
            using var db = _contextFactory();
            var cutoffDate = DateTime.Now.AddYears(-years);
            return db.AuditLogs.AsNoTracking()
                .Include(a => a.Performer)
                .Where(a => a.PerformedAt < cutoffDate)
                .OrderBy(a => a.PerformedAt)
                .ToList();
        }

        public IReadOnlyList<QuanLyHangHoa.Models.AuditLog> GetLogsBefore(DateTime cutoffDate)
        {
            using var db = _contextFactory();
            return db.AuditLogs.AsNoTracking()
                .Include(a => a.Performer)
                .Where(a => a.PerformedAt < cutoffDate)
                .OrderBy(a => a.PerformedAt)
                .ToList();
        }

        public IReadOnlyList<QuanLyHangHoa.Models.AuditLog> GetLogsBetween(DateTime start, DateTime end)
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
            int count = logsToRemove.Count();
            db.AuditLogs.RemoveRange(logsToRemove);
            db.SaveChanges();
            return count;
        }
    }
}
