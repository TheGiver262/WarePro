using System;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockReversalService
    {
        private readonly Func<AppDbContext> _contextFactory;


        public StockReversalService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public int ReverseDocument(string docType, int docId, int userId)
        {
            using var db = _contextFactory();
            
            // 1. Find all ledger entries for this document
            var entries = db.StockLedgers
                .Where(l => l.SourceDocumentType == docType && l.SourceDocumentId == docId)
                .ToList();

            if (!entries.Any()) return 0;

            // 2. Create a reversal adjustment document
            var reversal = new StockAdjustment
            {
                DocumentCode = "REV-" + DateTime.Now.Ticks,
                AdjustmentType = "Reversal",
                ReasonCode = "SYSTEM-REVERSAL",
                Status = "Posted",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = DateTime.Now,
                ReferenceDocumentType = docType,
                ReferenceDocumentId = docId
            };

            db.StockAdjustments.Add(reversal);
            db.SaveChanges(); // Get reversal.Id

            // 3. Create compensating ledger entries
            foreach (var entry in entries)
            {
                var compensatingEntry = new StockLedger
                {
                    WarehouseId = entry.WarehouseId,
                    ProductId = entry.ProductId,
                    ProductSerialId = entry.ProductSerialId,
                    SourceDocumentType = "StockAdjustment",
                    SourceDocumentId = reversal.Id,
                    MovementType = entry.MovementType == "In" ? "Out" : "In",
                    Quantity = entry.Quantity,
                    PostedBy = userId,
                    PostedAt = DateTime.Now
                };
                db.StockLedgers.Add(compensatingEntry);
            }

            db.SaveChanges();
            return reversal.Id;
        }

        public int ReversePostedLedgerDocument(string docType, int docId, int userId)
        {
            return ReverseDocument(docType, docId, userId);
        }
    }
}
