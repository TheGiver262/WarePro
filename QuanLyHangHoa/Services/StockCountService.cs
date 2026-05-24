using System;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockCountService
    {
        private readonly Func<AppDbContext> _contextFactory;


        public StockCountService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void CreateSession(StockCountSession session)
        {
            using var db = _contextFactory();
            db.StockCountSessions.Add(session);
            db.SaveChanges();
            AddAudit(db, "CREATE", session.Id, null, Serialize(session), session.CreatedBy);
        }

        public void ProcessResults(int sessionId, int userId)
        {
            using var db = _contextFactory();
            var session = db.StockCountSessions
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == sessionId);

            if (session == null || session.Status != "đã kiểm kê") return;

            var beforeJson = Serialize(session);

            // 1. Create adjustment for variances
            var adjustment = new StockAdjustment
            {
                DocumentCode = "ADJ-CNT-" + sessionId,
                WarehouseId = session.WarehouseId,
                AdjustmentType = "StockCount",
                ReasonCode = "SYSTEM-COUNT",
                Status = "đã ghi sổ",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = DateTime.Now,
                ReferenceDocumentType = "StockCountSession",
                ReferenceDocumentId = sessionId
            };

            db.StockAdjustments.Add(adjustment);
            db.SaveChanges();

            // 2. Create adjustment lines and ledger entries
            foreach (var line in session.Lines!)
            {
                if (line.VarianceQuantity == 0) continue;

                var adjLine = new StockAdjustmentLine
                {
                    AdjustmentId = adjustment.Id,
                    ProductId = line.ProductId,
                    QuantityDelta = Math.Abs(line.VarianceQuantity),
                    BaseQuantityDelta = Math.Abs(line.VarianceQuantity),
                    Direction = line.VarianceQuantity > 0 ? "In" : "Out"
                };
                db.StockAdjustmentLines.Add(adjLine);

                var ledger = new StockLedger
                {
                    WarehouseId = session.WarehouseId,
                    ProductId = line.ProductId,
                    SourceDocumentType = "StockAdjustment",
                    SourceDocumentId = adjustment.Id,
                    MovementType = line.VarianceQuantity > 0 ? "In" : "Out",
                    Quantity = Math.Abs(line.VarianceQuantity),
                    PostedBy = userId,
                    PostedAt = DateTime.Now
                };
                db.StockLedgers.Add(ledger);
            }

            session.Status = "hoàn thành";
            session.PostedBy = userId;
            session.PostedAt = DateTime.Now;
            db.SaveChanges();

            var afterJson = Serialize(session);
            AddAudit(db, "POST", session.Id, beforeJson, afterJson, userId);
        }

        private string Serialize(StockCountSession s)
        {
            return JsonSerializer.Serialize(new
            {
                s.Id,
                s.SessionCode,
                s.WarehouseId,
                s.Status,
                s.CountDate,
                s.CreatedBy,
                s.PostedBy,
                s.PostedAt
            }, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            });
        }

        private void AddAudit(AppDbContext db, string action, int entityId, string? before, string? after, int performedBy)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "StockCountSession",
                EntityId = entityId,
                ActionCode = action,
                BeforeJson = before,
                AfterJson = after,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            db.SaveChanges();
        }
    }
}
