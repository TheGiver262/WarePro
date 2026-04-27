using System;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockReversalService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public StockReversalService()
            : this(() => new AppDbContext())
        {
        }

        public StockReversalService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public int ReversePostedLedgerDocument(Guid documentId, string reason, int reversedBy)
        {
            using var db = _contextFactory();
            var ledgers = db.StockLedgers
                .Where(ledger => ledger.DocumentId == documentId)
                .ToList();

            if (ledgers.Count == 0)
            {
                throw new InventoryDomainException($"Ledger document {documentId} does not exist.");
            }

            var warehouseIds = ledgers.Select(ledger => ledger.WarehouseId).Distinct().ToArray();
            if (warehouseIds.Length != 1)
            {
                throw new InventoryDomainException("Only single-warehouse ledger documents can be reversed.");
            }

            var adjustment = new StockAdjustment
            {
                DocumentCode = $"REV-{documentId:N}"[..24],
                WarehouseId = warehouseIds[0],
                AdjustmentType = "Reversal",
                Status = StockDocumentStatus.Approved.ToString(),
                ReferenceDocumentCode = documentId.ToString(),
                ReasonCode = reason,
                CreatedBy = reversedBy,
                PostedBy = reversedBy
            };

            foreach (var ledger in ledgers)
            {
                var reverseDirection = ParseDirection(ledger.Direction) == StockLedgerDirection.In
                    ? StockLedgerDirection.Out
                    : StockLedgerDirection.In;
                var signedQuantity = reverseDirection == StockLedgerDirection.In
                    ? ledger.Quantity
                    : -ledger.Quantity;

                adjustment.Lines.Add(new StockAdjustmentLine
                {
                    ProductId = ledger.ProductId,
                    QuantityDelta = signedQuantity,
                    BaseQuantityDelta = signedQuantity,
                    Direction = reverseDirection.ToString()
                });
            }

            new StockAdjustmentService(_contextFactory).Post(adjustment);
            return adjustment.Id;
        }

        private static StockLedgerDirection ParseDirection(string direction)
        {
            return Enum.TryParse<StockLedgerDirection>(direction, out var parsed)
                ? parsed
                : throw new InventoryDomainException($"Unsupported stock ledger direction {direction}.");
        }
    }
}
