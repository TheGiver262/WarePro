using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class StockCountMutationServiceTests
{
    [Fact]
    public void UpdateDraft_updates_counted_values_without_changing_status()
    {
        using var connection = CreateDatabase(out var sessionId, out var lineId, out var rowVersion);
        var service = new StockCountService(() => CreateContext(connection));

        service.UpdateDraft(
            sessionId,
            new[]
            {
                new StockCountLine
                {
                    Id = lineId,
                    RowVersion = rowVersion,
                    CountedQuantity = 7,
                    SerialNumbers = "SN-1,SN-2"
                }
            },
            userId: 1);

        using var db = CreateContext(connection);
        var session = db.StockCountSessions.Single(item => item.Id == sessionId);
        var line = db.StockCountLines.Single(item => item.Id == lineId);
        Assert.Equal("nh\u00e1p", session.Status);
        Assert.Equal(7, line.CountedQuantity);
        Assert.Equal(2, line.VarianceQuantity);
        Assert.Equal("SN-1,SN-2", line.SerialNumbers);
    }

    [Fact]
    public void CommitSession_updates_counted_values_and_marks_session_counted()
    {
        using var connection = CreateDatabase(out var sessionId, out var lineId, out var rowVersion);
        var service = new StockCountService(() => CreateContext(connection));

        service.CommitSession(
            sessionId,
            new[]
            {
                new StockCountLine
                {
                    Id = lineId,
                    RowVersion = rowVersion,
                    CountedQuantity = 4
                }
            },
            userId: 1);

        using var db = CreateContext(connection);
        var session = db.StockCountSessions.Single(item => item.Id == sessionId);
        var line = db.StockCountLines.Single(item => item.Id == lineId);
        Assert.Equal("\u0111\u00e3 ki\u1ec3m k\u00ea", session.Status);
        Assert.Equal(4, line.CountedQuantity);
        Assert.Equal(-1, line.VarianceQuantity);
    }

    private static SqliteConnection CreateDatabase(out int sessionId, out int lineId, out byte[] rowVersion)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        db.Products.Add(new Product
        {
            Id = 100,
            ProductCode = "COUNT-P100",
            DisplayName = "Count product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10,
            IsActive = true
        });
        var session = new StockCountSession
        {
            SessionCode = "COUNT-MUT-001",
            WarehouseId = 1,
            CountDate = DateTime.UtcNow,
            Status = "nh\u00e1p",
            CreatedBy = 1,
            Lines = new List<StockCountLine>
            {
                new()
                {
                    ProductId = 100,
                    SystemQuantity = 5,
                    CountedQuantity = -1,
                    VarianceQuantity = 0
                }
            }
        };
        db.StockCountSessions.Add(session);
        db.SaveChanges();
        sessionId = session.Id;
        lineId = session.Lines.Single().Id;
        rowVersion = session.Lines.Single().RowVersion.ToArray();
        return connection;
    }

    private static AppDbContext CreateContext(SqliteConnection connection) =>
        DatabaseHelper.CreateContext(connection);
}
