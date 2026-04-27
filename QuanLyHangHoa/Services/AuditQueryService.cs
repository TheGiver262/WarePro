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
        Guid DocumentId,
        DateTime OccurredAt,
        string Action,
        int UserId,
        int? ProductId,
        int? WarehouseId,
        int? Quantity);

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

        public IReadOnlyList<AuditTimelineEntry> GetDocumentTimeline(Guid documentId)
        {
            using var db = _contextFactory();
            var auditEntries = db.AuditLogs
                .Where(audit => audit.DocumentId == documentId)
                .Select(audit => new AuditTimelineEntry(
                    AuditTimelineEntryKind.Audit,
                    audit.DocumentId,
                    audit.PerformedAt,
                    audit.ActionCode,
                    audit.PerformedByUserId,
                    null,
                    null,
                    null));

            var ledgerEntries = db.StockLedgers
                .Where(ledger => ledger.DocumentId == documentId)
                .Select(ledger => new AuditTimelineEntry(
                    AuditTimelineEntryKind.StockLedger,
                    ledger.DocumentId,
                    ledger.PostedAt,
                    ledger.Direction,
                    ledger.PostedByUserId,
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
                    ledger.DocumentId,
                    ledger.PostedAt,
                    ledger.Direction,
                    ledger.PostedByUserId,
                    ledger.ProductId,
                    ledger.WarehouseId,
                    ledger.Quantity))
                .ToList();
        }
    }
}
