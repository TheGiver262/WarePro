using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class WarrantyService
    {
        private readonly Func<AppDbContext> _contextFactory;

        public WarrantyService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // --- Warranty Coverage (Policy/Period) ---

        public List<WarrantyCoverage> GetAllCoverages()
        {
            using var db = _contextFactory();
            return db.WarrantyCoverages.AsNoTracking()
                .Include(c => c.ProductSerial)
                    .ThenInclude(s => s!.Product)
                .ToList();
        }

        public void AddCoverage(WarrantyCoverage coverage)
        {
            using var db = _contextFactory();
            db.WarrantyCoverages.Add(coverage);
            db.SaveChanges();
        }

        // --- Warranty Claims (Requests) ---

        public List<WarrantyClaim> GetAllClaims()
        {
            using var db = _contextFactory();
            return db.WarrantyClaims.AsNoTracking()
                .Include(c => c.ProductSerial)
                    .ThenInclude(s => s!.Product)
                .Include(c => c.WarrantyCoverage)
                .ToList();
        }

        public void AddClaim(WarrantyClaim claim, int performedByUserId)
        {
            using var db = _contextFactory();
            claim.ProcessedBy = performedByUserId;
            claim.ReceivedDate = DateTime.Now;
            claim.Status = "Pending";

            db.WarrantyClaims.Add(claim);
            db.SaveChanges();

            AddAudit(db, "WarrantyClaim", claim.Id, "Create", performedByUserId);
            db.SaveChanges();
        }

        public void UpdateClaimStatus(int claimId, string status, string? resolution, int performedByUserId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId);
            if (claim == null) return;

            claim.Status = status;
            claim.ProcessingNote = resolution;
            
            if (status == "Completed" || status == "Rejected")
            {
                claim.ClosedDate = DateTime.Now;
                claim.ApprovedBy = performedByUserId;
            }

            db.SaveChanges();
            AddAudit(db, "WarrantyClaim", claimId, $"StatusUpdate:{status}", performedByUserId);
            db.SaveChanges();
        }

        private void AddAudit(AppDbContext db, string entityName, int entityId, string action, int performedByUserId)
        {
            db.AuditLogs.Add(new AuditLog
            {
                EntityName = entityName,
                EntityId = entityId,
                ActionCode = action,
                PerformedBy = performedByUserId,
                PerformedAt = DateTime.Now
            });
        }
    }
}
