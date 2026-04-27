using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockCountService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockCountService()
            : this(() => new AppDbContext())
        {
        }

        public StockCountService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public int CreateAdjustmentForDifferences(int sessionId, int createdBy)
        {
            using var db = _contextFactory();
            var session = db.StockCountSessions
                .Include(s => s.Lines)
                .SingleOrDefault(s => s.Id == sessionId)
                ?? throw new InventoryDomainException($"Stock count session {sessionId} does not exist.");

            if (session.Status != StockDocumentStatus.Approved.ToString())
            {
                throw new InventoryDomainException("Only approved stock count sessions can create adjustments.");
            }

            var differentLines = session.Lines
                .Where(line => line.DifferenceQuantity != 0)
                .ToList();

            if (differentLines.Count == 0)
            {
                throw new InventoryDomainException("Stock count session has no differences.");
            }

            var adjustment = new StockAdjustment
            {
                DocumentCode = $"ADJ-{session.SessionCode}",
                WarehouseId = session.WarehouseId,
                AdjustmentType = "StockCount",
                Status = StockDocumentStatus.Draft.ToString(),
                ReferenceDocumentCode = session.SessionCode,
                ReasonCode = "CountDifference",
                CreatedBy = createdBy
            };

            foreach (var countLine in differentLines)
            {
                var direction = countLine.DifferenceQuantity > 0
                    ? StockLedgerDirection.In
                    : StockLedgerDirection.Out;
                var quantity = Math.Abs(countLine.DifferenceQuantity);

                adjustment.Lines.Add(new StockAdjustmentLine
                {
                    ProductId = countLine.ProductId,
                    QuantityDelta = direction == StockLedgerDirection.In ? quantity : -quantity,
                    BaseQuantityDelta = direction == StockLedgerDirection.In ? quantity : -quantity,
                    Direction = direction.ToString()
                });
            }

            db.StockAdjustments.Add(adjustment);
            db.SaveChanges();
            return adjustment.Id;
        }
    }
}
