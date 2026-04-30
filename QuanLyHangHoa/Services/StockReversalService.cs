using System;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class StockReversalService
    {
        public void ReverseDocument(string docType, int docId, int userId)
        {
            using var db = new AppDbContext();
            
            // 1. Find all ledger entries for this document
            var entries = db.StockLedgers
                .Where(l => l.SourceDocumentType == docType && l.SourceDocumentId == docId)
                .ToList();

            if (!entries.Any()) return;

            // 2. Create a reversal adjustment document
            var reversal = new StockAdjustment
            {
                DocumentCode = "REV-" + DateTime.Now.Ticks,
                AdjustmentType = "Reversal",
                ReasonCode = "SYSTEM-REVERSAL",
                Status = "Posted",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = DateTime.UtcNow,
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
                    PostedAt = DateTime.UtcNow
                };
                db.StockLedgers.Add(compensatingEntry);
            }

            db.SaveChanges();
        }

        public void ReversePostedLedgerDocument(string docType, int docId, int userId)
        {
            ReverseDocument(docType, docId, userId);
        }

        public int ReversePostedLedgerDocument(Guid documentId, string reason, int userId)
        {
            // In the inventory engine, documents are identified by Guid (CommandId/SourceDocumentId).
            // This implementation maps the Guid-based reversal to the existing ledger reversal logic.
            
            using var db = new AppDbContext();
            
            // 1. Find all ledger entries with this SourceDocumentId (Guid string)
            var docIdStr = documentId.ToString();
            var entries = db.StockLedgers
                .Where(l => l.SourceDocumentId.ToString() == docIdStr)
                .ToList();

            if (!entries.Any()) throw new InvalidOperationException($"No ledger entries found for document {documentId}.");

            // 2. Create a reversal adjustment document
            var reversal = new StockAdjustment
            {
                DocumentCode = "REV-" + DateTime.Now.Ticks,
                AdjustmentType = "Reversal",
                ReasonCode = reason,
                Status = "Posted",
                CreatedBy = userId,
                PostedBy = userId,
                PostedAt = DateTime.UtcNow,
                ReferenceDocumentType = entries.First().SourceDocumentType,
                ReferenceDocumentId = entries.First().SourceDocumentId
            };

            db.StockAdjustments.Add(reversal);
            db.SaveChanges();

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
                    PostedAt = DateTime.UtcNow
                };
                db.StockLedgers.Add(compensatingEntry);
            }

            db.SaveChanges();
            return reversal.Id;
        }
    }
}
