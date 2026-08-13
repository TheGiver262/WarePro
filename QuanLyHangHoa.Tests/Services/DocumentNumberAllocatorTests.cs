using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;
using Xunit;

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
}
