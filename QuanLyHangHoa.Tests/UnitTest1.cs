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
