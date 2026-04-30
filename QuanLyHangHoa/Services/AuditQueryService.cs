using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;

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

        public AuditQueryService()
            : this(() => new AppDbContext())
        {
        }

        public AuditQueryService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public IReadOnlyList<AuditTimelineEntry> GetEntityTimeline(string entityName, int entityId)
        {
            using var db = _contextFactory();
            var auditEntries = db.AuditLogs
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

            var ledgerEntries = db.StockLedgers
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
            return db.StockLedgers
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

            var auditEntries = db.AuditLogs
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

            var ledgerEntries = db.StockLedgers
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
    }
}
