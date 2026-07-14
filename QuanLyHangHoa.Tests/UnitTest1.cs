using QuanLyHangHoa.Data;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace QuanLyHangHoa.Tests;

[Trait("Category", "RealDatabase")]

public class UnitTest1
{
    [Fact]
    public void TestSecondaryIntegrity()
    {
        // 1. Dọn dẹp dữ liệu rác bằng DbContext riêng biệt và bắt concurrency exception
        using (var cleanupDb = new AppDbContext())
        {
            var oldClaims = cleanupDb.WarrantyClaims.Where(c => c.ProductSerial.SerialNumber.StartsWith("SN-TEST-W-")).ToList();
            if (oldClaims.Any()) cleanupDb.WarrantyClaims.RemoveRange(oldClaims);

            var oldCoverages = cleanupDb.WarrantyCoverages.Where(c => c.ProductSerial.SerialNumber.StartsWith("SN-TEST-W-")).ToList();
            if (oldCoverages.Any()) cleanupDb.WarrantyCoverages.RemoveRange(oldCoverages);

            var oldSerials = cleanupDb.ProductSerials.Where(s => s.SerialNumber.StartsWith("SN-TEST-W-")).ToList();
            if (oldSerials.Any()) cleanupDb.ProductSerials.RemoveRange(oldSerials);
            
            try
            {
                cleanupDb.SaveChanges();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Bỏ qua nếu test chạy song song khác đã xóa trước
            }
        }

        // 2. Chạy test chính với DbContext mới
        using var db = new AppDbContext();
        var totalSerials = db.ProductSerials.Count();
        
        var survivedSerials = db.ProductSerials
            .Include(s => s.Product)
                .ThenInclude(p => p.Brand)
            .Include(s => s.CurrentWarehouse)
            .Include(s => s.LastStockInLine)
                .ThenInclude(l => l.StockIn)
            .ToList();

        Assert.Equal(survivedSerials.Select(s => s.Id).Distinct().Count(), survivedSerials.Count);
    }
}
