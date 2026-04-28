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
    }
}
