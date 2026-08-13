using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;
using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public sealed class DocumentNumberAllocatorTests
{
    [Fact]
    public async Task AllocateAsync_increments_per_document_type_and_business_date()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        db.Database.EnsureCreated();
        var date = new DateOnly(2026, 8, 13);

        var first = await DocumentNumberAllocator.AllocateAsync(
            db, "StockIn", "IN", date, CancellationToken.None);
        var second = await DocumentNumberAllocator.AllocateAsync(
            db, "StockIn", "IN", date, CancellationToken.None);
        var otherType = await DocumentNumberAllocator.AllocateAsync(
            db, "StockOut", "OUT", date, CancellationToken.None);
        var otherDate = await DocumentNumberAllocator.AllocateAsync(
            db, "StockIn", "IN", date.AddDays(1), CancellationToken.None);

        Assert.Equal("IN-20260813-000001", first);
        Assert.Equal("IN-20260813-000002", second);
        Assert.Equal("OUT-20260813-000001", otherType);
        Assert.Equal("IN-20260814-000001", otherDate);
    }

    [Fact]
    public async Task AllocateAsync_rolls_back_with_the_callers_transaction()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = DatabaseHelper.CreateContext(connection);
        db.Database.EnsureCreated();
        var date = new DateOnly(2026, 8, 13);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var rolledBack = await DocumentNumberAllocator.AllocateAsync(
                db, "StockIn", "IN", date, CancellationToken.None);
            Assert.Equal("IN-20260813-000001", rolledBack);
            await transaction.RollbackAsync();
        }

        var committed = await DocumentNumberAllocator.AllocateAsync(
            db, "StockIn", "IN", date, CancellationToken.None);
        Assert.Equal("IN-20260813-000001", committed);
    }

    [Fact]
    public void Active_callers_allocate_through_the_executor_context()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        foreach (var file in new[]
                 {
                     "StockInService.cs",
                     "StockOutService.cs",
                     "StockAdjustmentService.cs",
                     "InvoiceService.cs",
                     "ProductService.cs"
                 })
        {
            var source = File.ReadAllText(Path.Combine(repoRoot, "QuanLyHangHoa", "Services", file));
            Assert.Contains("DocumentNumberAllocator.AllocateAsync(", source, StringComparison.Ordinal);
            Assert.DoesNotContain("numberingDb", source, StringComparison.Ordinal);
        }
    }
}
