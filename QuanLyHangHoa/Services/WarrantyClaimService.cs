using System;
using System.Collections.Generic;
using System.Linq;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class WarrantyClaimService
    {
        private readonly Func<AppDbContext> _contextFactory;


        public WarrantyClaimService(Func<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public void CreateClaim(WarrantyClaim claim)
        {
            using var db = _contextFactory();
            db.WarrantyClaims.Add(claim);
            db.SaveChanges();
        }

        public void UpdateClaim(WarrantyClaim claim)
        {
            using var db = _contextFactory();
            db.WarrantyClaims.Update(claim);
            db.SaveChanges();
        }

        public void ResolveClaim(int claimId, string resolutionType, string technicalConclusion, int approverId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId) 
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            claim.ResolutionType = resolutionType;
            claim.TechnicalConclusion = technicalConclusion;
            claim.ApprovedBy = approverId;
            claim.Status = "Ready";
            db.SaveChanges();
        }

        public void CloseClaim(int claimId, string note)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            claim.ProcessingNote = note;
            claim.Status = "Closed";
            claim.ClosedDate = DateTime.Now;
            db.SaveChanges();
        }

        public int CreateClaim(string claimCode, string serialNumber, string problemDescription, int userId)
        {
            using var db = _contextFactory();
            var serial = db.ProductSerials.FirstOrDefault(s => s.SerialNumber == serialNumber)
                ?? throw new InvalidOperationException($"Serial {serialNumber} not found.");

            var coverage = db.WarrantyCoverages.FirstOrDefault(c => c.ProductSerialId == serial.Id && c.CoverageStatus == "Active" && c.WarrantyEndDate >= DateTime.Now)
                ?? throw new InvalidOperationException($"No active warranty coverage found for serial {serialNumber}.");

            var hasOpenClaim = db.WarrantyClaims.Any(c => c.ProductSerialId == serial.Id && c.Status == "Open");
            if (hasOpenClaim)
            {
                throw new InvalidOperationException($"Serial {serialNumber} already has an open warranty claim.");
            }

            var claim = new WarrantyClaim
            {
                ClaimCode = claimCode,
                ProductSerialId = serial.Id,
                WarrantyCoverageId = coverage.Id,
                ProblemDescription = problemDescription,
                ReceivedDate = DateTime.Now,
                Status = "Open",
                ProcessedBy = userId
            };

            db.WarrantyClaims.Add(claim);
            db.SaveChanges();
            return claim.Id;
        }

        public void CompleteRepair(int claimId, string technicalConclusion, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            claim.TechnicalConclusion = technicalConclusion;
            claim.Status = "Ready";
            claim.ResolutionType = "Repair";
            claim.ApprovedBy = userId;
            db.SaveChanges();
        }

        public void SendToManufacturer(int claimId, string manufacturerNote, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            claim.ManufacturerResult = manufacturerNote;
            claim.Status = "ManufacturerWait";
            claim.ProcessedBy = userId;
            db.SaveChanges();
        }

        public void RejectClaim(int claimId, string reason, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            claim.RejectionReason = reason;
            claim.Status = "Rejected";
            claim.ResolutionType = "Reject";
            claim.ApprovedBy = userId;
            claim.ClosedDate = DateTime.Now;
            db.SaveChanges();
        }

        public void ReplaceSerial(int claimId, string replacementSerial, string conclusion, int userId)
        {
            using var db = _contextFactory();
            var claim = db.WarrantyClaims.Find(claimId)
                ?? throw new InvalidOperationException($"Claim {claimId} not found.");

            var newSerial = db.ProductSerials.FirstOrDefault(s => s.SerialNumber == replacementSerial)
                ?? throw new InvalidOperationException($"Replacement serial {replacementSerial} not found.");

            claim.ReplacementSerialId = newSerial.Id;
            claim.TechnicalConclusion = conclusion;
            claim.Status = "Ready";
            claim.ResolutionType = "Replace";
            claim.ApprovedBy = userId;
            db.SaveChanges();
        }

        public WarrantyCoverage? GetCoverageBySerial(string serialNumber)
        {
            using var db = _contextFactory();
            var serial = db.ProductSerials
                .FirstOrDefault(s => s.SerialNumber == serialNumber);

            if (serial == null) return null;

            return db.WarrantyCoverages
                .FirstOrDefault(c => c.ProductSerialId == serial.Id && c.CoverageStatus == "Active" && c.WarrantyEndDate >= DateTime.Now);
        }
    }
}
