using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockCountService
    {
        public void CreateSession(StockCountSession session)
        {
            using var db = new AppDbContext();
            db.StockCountSessions.Add(session);
            db.SaveChanges();
        }

        public void ProcessResults(int sessionId, int userId)
        {
            using var db = new AppDbContext();
            var session = db.StockCountSessions
                .Include(s => s.Lines)
                .FirstOrDefault(s => s.Id == sessionId);

            if (session == null || session.Status != "Counted") return;

            // 1. Create adjustment for variances
            var adjustment = new StockAdjustment
            {
                DocumentCode = "ADJ-CNT-" + sessionId,
                WarehouseId = session.WarehouseId,
                AdjustmentType = "StockCount",
                ReasonCode = "SYSTEM-COUNT",
                Status = "Posted",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = DateTime.UtcNow,
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
                    PostedAt = DateTime.UtcNow
                };
                db.StockLedgers.Add(ledger);
            }

            session.Status = "Completed";
            db.SaveChanges();
        }
    }
}
