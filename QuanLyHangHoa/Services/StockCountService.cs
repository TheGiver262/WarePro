using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public sealed record StockCountInput(int ProductId, decimal CountedQuantity);

    public class StockCountService
    {
        private readonly Func<AppDbContext> _contextFactory;
        private readonly Func<DateTime> _clock;

        public StockCountService()
            : this(() => new AppDbContext(), () => DateTime.Now)
        {
        }

        public StockCountService(Func<AppDbContext> contextFactory)
            : this(contextFactory, () => DateTime.Now)
        {
        }

        public StockCountService(Func<AppDbContext> contextFactory, Func<DateTime> clock)
        {
            _contextFactory = contextFactory;
            _clock = clock;
        }

        public int CreateApprovedSession(
            string sessionCode,
            int warehouseId,
            DateTime countDate,
            int createdBy,
            IEnumerable<StockCountInput> inputs)
        {
            var inputList = inputs.ToList();
            if (inputList.Count == 0)
            {
                throw new InventoryDomainException("Stock count session must have at least one line.");
            }

            if (inputList.Any(input => input.CountedQuantity < 0))
            {
                throw new InventoryDomainException("Counted quantity cannot be negative.");
            }

            using var db = _contextFactory();
            var now = _clock();
            var session = new StockCountSession
            {
                SessionCode = sessionCode,
                WarehouseId = warehouseId,
                Status = StockDocumentStatus.Approved.ToString(),
                CountDate = countDate,
                CreatedBy = createdBy,
                ApprovedBy = createdBy,
                ApprovedAt = now
            };

            foreach (var input in inputList)
            {
                var systemQuantity = db.StockBalances
                    .Where(balance => balance.ProductId == input.ProductId && balance.WarehouseId == warehouseId)
                    .Select(balance => (decimal)balance.OnHandQuantity)
                    .SingleOrDefault();

                session.Lines.Add(new StockCountLine
                {
                    ProductId = input.ProductId,
                    SystemQuantity = systemQuantity,
                    CountedQuantity = input.CountedQuantity,
                    DifferenceQuantity = input.CountedQuantity - systemQuantity
                });
            }

            db.StockCountSessions.Add(session);
            db.SaveChanges();
            return session.Id;
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
