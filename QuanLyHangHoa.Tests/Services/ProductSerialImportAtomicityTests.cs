using System.Data.Common;
using System.IO;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class ProductSerialImportAtomicityTests
{
    [Fact]
    public async Task Import_commits_before_a_concurrent_actor_revocation()
    {
        var connectionString = $"Data Source=serial-race-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=1";
        using var rootConnection = new SqliteConnection(connectionString);
        rootConnection.Open();
        SeedDatabase(rootConnection, includeWarehouse: true);
        var path = CreateWorkbook();
        var gate = new ProductQueryGateInterceptor();
        var commits = new CommitObserverInterceptor();

        try
        {
            var service = new ProductSerialImportService(
                () => CreateContext(connectionString, gate, commits));

            var importTask = service.ImportFromExcelAsync(path, actorId: 2);
            await gate.ProductQueryReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var revocationTask = RevokeActorWithRetryAsync(connectionString, actorId: 2);

            await Task.WhenAny(revocationTask, Task.Delay(250));
            gate.ContinueQuery.TrySetResult();

            var result = await importTask.WaitAsync(TimeSpan.FromSeconds(10));
            var revokedAt = await revocationTask.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(1, result.SuccessCount);
            Assert.NotNull(commits.LastCommittedAt);
            Assert.True(
                commits.LastCommittedAt <= revokedAt,
                $"Import committed at {commits.LastCommittedAt:O} after revocation committed at {revokedAt:O}.");
        }
        finally
        {
            gate.ContinueQuery.TrySetResult();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_rolls_back_all_writes_when_serial_insert_fails()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        SeedDatabase(connection, includeWarehouse: false);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TRIGGER FailSerialInsert
                BEFORE INSERT ON ProductSerial
                BEGIN
                    SELECT RAISE(ABORT, 'forced serial failure');
                END;
                """;
            command.ExecuteNonQuery();
        }
        var path = CreateWorkbook();

        try
        {
            var service = new ProductSerialImportService(() => DatabaseHelper.CreateContext(connection));

            var result = await service.ImportFromExcelAsync(path, actorId: 2);

            Assert.Equal(0, result.SuccessCount);
            Assert.Contains("forced serial failure", result.Message);
            using var db = DatabaseHelper.CreateContext(connection);
            Assert.Empty(db.Warehouses);
            Assert.Empty(db.StockIns);
            Assert.Empty(db.StockInLines);
            Assert.Empty(db.ProductSerials);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<DateTimeOffset> RevokeActorWithRetryAsync(string connectionString, int actorId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var db = CreateContext(connectionString);
                var actor = await db.AppUsers.SingleAsync(user => user.Id == actorId);
                actor.IsActive = false;
                await db.SaveChangesAsync();
                return DateTimeOffset.UtcNow;
            }
            catch (Exception ex) when (
                ex is SqliteException { SqliteErrorCode: 5 or 6 }
                || ex.InnerException is SqliteException { SqliteErrorCode: 5 or 6 })
            {
                await Task.Delay(10);
            }
        }

        throw new TimeoutException("Actor revocation did not complete before the test deadline.");
    }

    private static void SeedDatabase(SqliteConnection connection, bool includeWarehouse)
    {
        using var db = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(db);
        if (!includeWarehouse)
        {
            db.Warehouses.RemoveRange(db.Warehouses);
        }
        else
        {
            db.Warehouses.Single(item => item.Id == 1).IsDefault = true;
        }

        db.Products.Add(new Product
        {
            Id = 100,
            ProductCode = "P100",
            DisplayName = "Serial product",
            CategoryId = 1,
            BrandId = 1,
            DefaultUnitId = 1,
            DefaultPrice = 10m,
            IsActive = true,
            IsSerialTracked = true
        });
        db.SaveChanges();
    }

    private static AppDbContext CreateContext(
        string connectionString,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new AppDbContext(options);
    }

    private static string CreateWorkbook()
    {
        var path = Path.Combine(Path.GetTempPath(), $"serial-atomic-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var products = workbook.Worksheets.Add("S\u1ea3n ph\u1ea9m");
        products.Cell(1, 1).Value = "id";
        products.Cell(1, 2).Value = "ProductCode";
        products.Cell(2, 1).Value = "mongo-100";
        products.Cell(2, 2).Value = "P100";
        var serials = workbook.Worksheets.Add("Serial");
        serials.Cell(1, 1).Value = "SerialCode";
        serials.Cell(1, 2).Value = "ProductId";
        serials.Cell(2, 1).Value = "SN-ATOMIC-001";
        serials.Cell(2, 2).Value = "mongo-100";
        workbook.SaveAs(path);
        return path;
    }

    private sealed class ProductQueryGateInterceptor : DbCommandInterceptor
    {
        public TaskCompletionSource ProductQueryReached { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ContinueQuery { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM \"Product\"", StringComparison.Ordinal))
            {
                ProductQueryReached.TrySetResult();
                await ContinueQuery.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }

            return result;
        }
    }

    private sealed class CommitObserverInterceptor : DbTransactionInterceptor
    {
        public DateTimeOffset? LastCommittedAt { get; private set; }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        {
            LastCommittedAt = DateTimeOffset.UtcNow;
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            LastCommittedAt = DateTimeOffset.UtcNow;
            return Task.CompletedTask;
        }
    }
}
