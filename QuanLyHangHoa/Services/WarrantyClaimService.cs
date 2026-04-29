using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Inventory;
using QuanLyHangHoa.Models;

namespace QuanLyHangHoa.Services
{
    public class WarrantyClaimService
    {
        private static readonly string[] OpenStatuses =
        {
            "Checking",
            "InRepair",
            "SentToManufacturer",
            "WaitingDecision",
            "ApprovedForReplacement",
            "Replaced",
            "ReturnedToCustomer"
        };

        private readonly Func<AppDbContext> _contextFactory;
        private readonly Func<DateTime> _clock;

        public WarrantyClaimService()
            : this(() => new AppDbContext(), () => DateTime.Now)
        {
        }

        public WarrantyClaimService(Func<AppDbContext> contextFactory, Func<DateTime> clock)
        {
            _contextFactory = contextFactory;
            _clock = clock;
        }

        public int CreateClaim(string claimCode, string serialNumber, string problemDescription, int receivedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var now = _clock();
            var serial = db.ProductSerials.SingleOrDefault(s => s.SerialNumber == serialNumber && !s.IsDeleted)
                ?? throw new InvalidOperationException($"Serial {serialNumber} does not exist.");

            var coverage = db.WarrantyCoverages
                .SingleOrDefault(c =>
                    c.ProductSerialId == serial.Id &&
                    c.CoverageStatus == "Active" &&
                    c.WarrantyStartDate <= now &&
                    c.WarrantyEndDate >= now)
                ?? throw new InvalidOperationException($"Serial {serialNumber} has no active warranty coverage.");

            var hasOpenClaim = db.WarrantyClaims.Any(claim =>
                claim.ProductSerialId == serial.Id &&
                OpenStatuses.Contains(claim.ClaimStatus));
            if (hasOpenClaim)
            {
                throw new InvalidOperationException($"Serial {serialNumber} already has an open warranty claim.");
            }

            var claim = new WarrantyClaim
            {
                ClaimCode = claimCode,
                WarrantyCoverageId = coverage.Id,
                ProductSerialId = serial.Id,
                ReceivedDate = now,
                ClaimStatus = "Checking",
                ProblemDescription = problemDescription,
                ProcessedBy = receivedBy
            };

            serial.Status = SerialStatus.InWarrantyProcess.ToString();
            db.WarrantyClaims.Add(claim);
            db.SaveChanges();
            transaction.Commit();
            return claim.Id;
        }

        public void SendToManufacturer(int claimId, string note, int processedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var claim = GetOpenClaim(db, claimId);
            var serial = db.ProductSerials.Single(serial => serial.Id == claim.ProductSerialId);

            claim.ClaimStatus = "SentToManufacturer";
            claim.ProcessingNote = note;
            claim.ProcessedBy = processedBy;
            serial.Status = SerialStatus.ReturnedToManufacturer.ToString();

            db.SaveChanges();
            transaction.Commit();
        }

        public void CompleteRepair(int claimId, string technicalConclusion, int processedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var now = _clock();
            var claim = GetOpenClaim(db, claimId);
            var serial = db.ProductSerials.Single(serial => serial.Id == claim.ProductSerialId);

            claim.ClaimStatus = "ReturnedToCustomer";
            claim.TechnicalConclusion = technicalConclusion;
            claim.ProcessedBy = processedBy;
            claim.ClosedDate = now;
            serial.Status = SerialStatus.Sold.ToString();

            db.SaveChanges();
            transaction.Commit();
        }

        public void RejectClaim(int claimId, string rejectionReason, int processedBy)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                throw new InvalidOperationException("Warranty rejection reason is required.");
            }

            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var now = _clock();
            var claim = GetOpenClaim(db, claimId);
            var serial = db.ProductSerials.Single(serial => serial.Id == claim.ProductSerialId);

            claim.ClaimStatus = "ReturnedToCustomer";
            claim.RejectionReason = rejectionReason;
            claim.ProcessedBy = processedBy;
            claim.ClosedDate = now;
            serial.Status = SerialStatus.Sold.ToString();

            db.SaveChanges();
            transaction.Commit();
        }

        public void ReplaceSerial(int claimId, string replacementSerialNumber, string technicalConclusion, int processedBy)
        {
            using var db = _contextFactory();
            using var transaction = db.Database.BeginTransaction();
            var now = _clock();
            var claim = GetOpenClaim(db, claimId);
            var coverage = db.WarrantyCoverages.Single(coverage => coverage.Id == claim.WarrantyCoverageId);
            var originalSerial = db.ProductSerials.Single(serial => serial.Id == claim.ProductSerialId);
            var replacementSerial = db.ProductSerials.SingleOrDefault(serial =>
                    serial.SerialNumber == replacementSerialNumber && !serial.IsDeleted)
                ?? throw new InvalidOperationException($"Replacement serial {replacementSerialNumber} does not exist.");

            if (replacementSerial.Status != SerialStatus.InStock.ToString())
            {
                throw new InvalidOperationException($"Replacement serial {replacementSerialNumber} is not in stock.");
            }

            claim.ClaimStatus = "Replaced";
            claim.ReplacementSerialId = replacementSerial.Id;
            claim.TechnicalConclusion = technicalConclusion;
            claim.ProcessedBy = processedBy;
            claim.ClosedDate = now;

            originalSerial.Status = SerialStatus.Replaced.ToString();
            replacementSerial.Status = SerialStatus.Sold.ToString();
            replacementSerial.CurrentWarehouseId = null;

            db.WarrantyCoverages.Add(new WarrantyCoverage
            {
                ProductSerialId = replacementSerial.Id,
                CustomerId = coverage.CustomerId,
                SalesInvoiceId = coverage.SalesInvoiceId,
                WarrantyStartDate = now,
                WarrantyEndDate = coverage.WarrantyEndDate,
                CoverageStatus = "Active"
            });

            db.SaveChanges();
            transaction.Commit();
        }

        private static WarrantyClaim GetOpenClaim(AppDbContext db, int claimId)
        {
            var claim = db.WarrantyClaims.SingleOrDefault(claim => claim.Id == claimId)
                ?? throw new InvalidOperationException($"Warranty claim {claimId} does not exist.");

            if (!OpenStatuses.Contains(claim.ClaimStatus))
            {
                throw new InvalidOperationException($"Warranty claim {claimId} is already closed.");
            }

            return claim;
        }
    }
}
