using System.IO;
using System.Security.Cryptography;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public sealed class AuditArchiveServiceTests
{
    [Fact]
    public async Task Archive_persists_manifest_and_hash_without_deleting_rows()
    {
        using var connection = CreateDatabase(out var start, out var end);
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.txt");

        try
        {
            var service = new AuditLogService(() => DatabaseHelper.CreateContext(connection));

            var manifest = await service.ArchiveLogsAsync(
                start,
                end,
                actorId: 1,
                path,
                ExportIds,
                Guid.NewGuid());

            var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            using var assertContext = DatabaseHelper.CreateContext(connection);
            var stored = Assert.Single(assertContext.AuditArchiveManifests);
            Assert.Equal(manifest.Id, stored.Id);
            Assert.Equal(1, stored.ActorId);
            Assert.Equal(start, stored.RangeStartUtc);
            Assert.Equal(end, stored.RangeEndUtc);
            Assert.Equal(2, stored.RowCount);
            Assert.Equal(Path.GetFileName(path), stored.FileName);
            Assert.Equal(expectedHash, stored.Sha256Hash);
            Assert.Equal(3, assertContext.AuditLogs.Count());
            Assert.Contains(assertContext.AuditLogs, log => log.EntityId == 1);
            Assert.Contains(assertContext.AuditLogs, log => log.EntityId == 2);
            Assert.Contains(assertContext.AuditLogs, log => log.EntityId == 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Archive_replaying_same_operation_returns_one_manifest_without_exporting_twice()
    {
        using var connection = CreateDatabase(out var start, out var end);
        var path = Path.Combine(Path.GetTempPath(), $"audit-replay-{Guid.NewGuid():N}.txt");
        var operationId = Guid.NewGuid();
        var exportCalls = 0;

        try
        {
            var service = new AuditLogService(() => DatabaseHelper.CreateContext(connection));
            void Export(IReadOnlyList<AuditLog> logs, string outputPath)
            {
                exportCalls++;
                ExportIds(logs, outputPath);
            }

            var first = await service.ArchiveLogsAsync(
                start, end, 1, path, Export, operationId);
            var replay = await service.ArchiveLogsAsync(
                start, end, 1, path, Export, operationId);

            Assert.Equal(first.Id, replay.Id);
            Assert.Equal(1, exportCalls);
            using var db = DatabaseHelper.CreateContext(connection);
            Assert.Single(db.AuditArchiveManifests);
            Assert.Equal(3, db.AuditLogs.Count());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Archive_honors_pre_cancelled_token_without_file_or_database_changes()
    {
        using var connection = CreateDatabase(out var start, out var end);
        var path = Path.Combine(Path.GetTempPath(), $"audit-cancel-{Guid.NewGuid():N}.txt");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new AuditLogService(() => DatabaseHelper.CreateContext(connection));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ArchiveLogsAsync(
                start,
                end,
                1,
                path,
                ExportIds,
                Guid.NewGuid(),
                cancellation.Token));

        Assert.False(File.Exists(path));
        using var db = DatabaseHelper.CreateContext(connection);
        Assert.Equal(3, db.AuditLogs.Count());
        Assert.Empty(db.AuditArchiveManifests);
    }

    [Fact]
    public async Task Archive_keeps_recovery_file_and_manifest_when_final_rename_fails()
    {
        using var connection = CreateDatabase(out var start, out var end);
        var root = Path.Combine(Path.GetTempPath(), $"audit-recovery-{Guid.NewGuid():N}");
        var finalPath = Path.Combine(root, "archive-target");
        Directory.CreateDirectory(finalPath);
        var operationId = Guid.NewGuid();
        var service = new AuditLogService(() => DatabaseHelper.CreateContext(connection));

        try
        {
            var renameError = await Record.ExceptionAsync(() => service.ArchiveLogsAsync(
                start,
                end,
                1,
                finalPath,
                ExportIds,
                operationId));
            Assert.True(renameError is IOException or UnauthorizedAccessException);

            using var db = DatabaseHelper.CreateContext(connection);
            var manifest = Assert.Single(db.AuditArchiveManifests);
            Assert.Equal(operationId, manifest.OperationId);
            Assert.Equal(3, db.AuditLogs.Count());
            Assert.Contains(
                Directory.GetFiles(root),
                file => Path.GetFileName(file).Contains(operationId.ToString("N"), StringComparison.Ordinal));

            Directory.Delete(finalPath);
            var replay = await service.ArchiveLogsAsync(
                start,
                end,
                1,
                finalPath,
                ExportIds,
                operationId);

            Assert.Equal(manifest.Id, replay.Id);
            Assert.True(File.Exists(finalPath));
            Assert.DoesNotContain(
                Directory.GetFiles(root),
                file => Path.GetFileName(file).Contains(operationId.ToString("N"), StringComparison.Ordinal));
            Assert.Single(db.AuditArchiveManifests);
            Assert.Equal(3, db.AuditLogs.Count());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Archive_keeps_recovery_when_commit_verification_is_indeterminate()
    {
        using var connection = CreateDatabase(out var start, out var end);
        var root = Path.Combine(Path.GetTempPath(), $"audit-indeterminate-{Guid.NewGuid():N}");
        var finalPath = Path.Combine(root, "archive.txt");
        var operationId = Guid.NewGuid();
        var state = new AmbiguousCommitState();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(
                new ThrowAfterCommitInterceptor(state),
                new ThrowDuringVerificationInterceptor(state))
            .Options;
        var service = new AuditLogService(() => new AppDbContext(options));

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ArchiveLogsAsync(
                start,
                end,
                1,
                finalPath,
                ExportIds,
                operationId));

            Assert.Contains(
                Directory.GetFiles(root),
                file => Path.GetFileName(file).Contains(
                    operationId.ToString("N"),
                    StringComparison.Ordinal));

            using var db = DatabaseHelper.CreateContext(connection);
            Assert.Single(db.AuditArchiveManifests);
            Assert.Equal(3, db.AuditLogs.Count());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
    private static SqliteConnection CreateDatabase(out DateTime start, out DateTime end)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        end = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        using var seed = DatabaseHelper.CreateContext(connection);
        DatabaseHelper.SeedBasicData(seed);
        seed.AuditLogs.AddRange(
            NewLog(1, start.AddDays(1)),
            NewLog(2, end.AddDays(-1)),
            NewLog(3, end.AddDays(2)));
        seed.SaveChanges();
        return connection;
    }

    private static void ExportIds(IReadOnlyList<AuditLog> logs, string outputPath) =>
        File.WriteAllText(
            outputPath,
            string.Join(Environment.NewLine, logs.Select(log => log.Id)));

    private static AuditLog NewLog(int entityId, DateTime performedAt) => new()
    {
        EntityName = "Product",
        EntityId = entityId,
        ActionCode = "UPDATE",
        PerformedBy = 1,
        PerformedAt = performedAt
    };

    private sealed class AmbiguousCommitState
    {
        public int CommitCompleted;
    }

    private sealed class ThrowAfterCommitInterceptor(AmbiguousCommitState state) :
        DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Exchange(ref state.CommitCompleted, 1);
            throw new InvalidOperationException("Simulated ambiguous commit.");
        }
    }

    private sealed class ThrowDuringVerificationInterceptor(AmbiguousCommitState state) :
        DbCommandInterceptor
    {
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ThrowIfVerifying();
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ThrowIfVerifying();
            return ValueTask.FromResult(result);
        }

        private void ThrowIfVerifying()
        {
            if (Volatile.Read(ref state.CommitCompleted) == 1)
            {
                throw new InvalidOperationException("Simulated verification failure.");
            }
        }
    }
}
