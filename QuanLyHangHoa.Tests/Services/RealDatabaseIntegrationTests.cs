using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using Xunit;

namespace QuanLyHangHoa.Tests.Services
{
    public class RealDatabaseIntegrationTests
    {
        [Fact]
        public void Test_RealDatabase_UnitConversion()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=.\\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using (var db = new AppDbContext(options))
            {
                // Verify SP016 and UNIT004 (Thùng) exists
                var product = db.Products.FirstOrDefault(p => p.ProductCode == "SP016");
                Assert.NotNull(product);
                var unit = db.Units.FirstOrDefault(u => u.UnitCode == "UNIT004");
                Assert.NotNull(unit);

                var service = new StockInService(() => new AppDbContext(options));
                
                // Create draft stock in
                var stockIn = new StockIn
                {
                    DocumentCode = "SI-REAL-TEST-" + Guid.NewGuid().ToString().Substring(0, 8),
                    SupplierId = 1,
                    WarehouseId = 1,
                    PurposeCode = "Purchase",
                    CreatedAt = DateTime.Now
                };

                var lines = new List<StockInLine>
                {
                    new StockInLine
                    {
                        ProductId = product.Id,
                        UnitId = unit.Id,
                        Quantity = 2, // 2 Thùng
                        UnitPrice = 100000m
                    }
                };

                // Get current balance
                var balanceBefore = db.StockBalances.FirstOrDefault(b => b.ProductId == product.Id && b.WarehouseId == 1);
                decimal qtyBefore = balanceBefore?.OnHandQuantity ?? 0m;
                decimal qtyAvailBefore = balanceBefore?.AvailableQuantity ?? 0m;

                service.SaveDraft(stockIn, lines, 1);
                
                // Assert SaveDraft auto-populated BaseQuantity to 24 (2 * 12)
                var draftLine = db.StockInLines.FirstOrDefault(l => l.StockInId == stockIn.Id);
                Assert.NotNull(draftLine);
                Assert.Equal(24m, draftLine.BaseQuantity);

                // Post
                service.Post(stockIn.Id, 1);

                // Verify stock balance increased by 24 (not 2)
                if (balanceBefore != null)
                {
                    db.Entry(balanceBefore).Reload();
                }
                var balanceAfter = db.StockBalances.FirstOrDefault(b => b.ProductId == product.Id && b.WarehouseId == 1);
                Assert.NotNull(balanceAfter);
                Assert.Equal(qtyBefore + 24m, balanceAfter.OnHandQuantity);

                // Clean up draft and ledger entries to keep DB clean
                var dbStockIn = db.StockIns.Include(s => s.Lines).FirstOrDefault(s => s.Id == stockIn.Id);
                if (dbStockIn != null)
                {
                    db.StockInLines.RemoveRange(dbStockIn.Lines);
                    db.StockIns.Remove(dbStockIn);
                }

                // Clean up StockLedger entries for this document
                var ledgers = db.StockLedgers.Where(l => l.SourceDocumentType == "StockIn" && l.SourceDocumentId == stockIn.Id).ToList();
                db.StockLedgers.RemoveRange(ledgers);

                // Clean up AuditLogs for this document
                var audits = db.AuditLogs.Where(a => a.EntityName == "StockIn" && a.EntityId == stockIn.Id).ToList();
                db.AuditLogs.RemoveRange(audits);

                // Revert stock balance
                if (balanceBefore != null)
                {
                    balanceBefore.OnHandQuantity = qtyBefore;
                    balanceBefore.AvailableQuantity = qtyAvailBefore;
                }
                else if (balanceAfter != null)
                {
                    db.StockBalances.Remove(balanceAfter);
                }

                db.SaveChanges();
            }
        }

        [Fact]
        public void Test_RealDatabase_SearchSerials()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer("Server=.\\SQLEXPRESS;Database=ProductManagementDb;Trusted_Connection=True;TrustServerCertificate=True;")
                .Options;

            using (var db = new AppDbContext(options))
            {
                var results = new List<string>();
                int totalInDb = db.ProductSerials.Count();
                results.Add($"Total in DB: {totalInDb}");
                
                // Check Product foreign key
                int validProductCount = db.ProductSerials.Count(s => db.Products.Any(p => p.Id == s.ProductId));
                results.Add($"Serials with valid ProductId: {validProductCount}");

                // Check LastStockInLine foreign key
                int validStockInLineCount = db.ProductSerials.Count(s => db.StockInLines.Any(l => l.Id == s.LastStockInLineId));
                results.Add($"Serials with valid LastStockInLineId: {validStockInLineCount}");

                // Check both valid
                int bothValid = db.ProductSerials.Count(s => db.Products.Any(p => p.Id == s.ProductId) && db.StockInLines.Any(l => l.Id == s.LastStockInLineId));
                results.Add($"Serials with both ProductId and LastStockInLineId valid: {bothValid}");

                // Run SearchSerialsPaged to see how many it returns with skip=0, take=1000
                var service = new ProductSerialService(() => new AppDbContext(options));
                var pagedResults = service.SearchSerialsPaged(string.Empty, string.Empty, string.Empty, "All", null, null, string.Empty, 0, 1000);
                results.Add($"SearchSerialsPaged returned: {pagedResults.Count}");

                var statusCounts = db.ProductSerials
                    .GroupBy(s => s.CurrentStatus)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToList();
                    
                foreach (var sc in statusCounts)
                {
                    results.Add($"Status '{sc.Status}': {sc.Count}");
                }
                
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "SearchSerialsCount.txt"),
                    string.Join("\n", results));
            }
        }
    }
}
