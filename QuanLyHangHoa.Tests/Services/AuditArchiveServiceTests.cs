using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Models;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Tests.Helpers;

namespace QuanLyHangHoa.Tests.Services;

public class AuditArchiveServiceTests
{
    [Fact]
    public void Archive_persists_manifest_and_hash_before_deleting_rows()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        using (var seed = DatabaseHelper.CreateContext(connection))
        {
            DatabaseHelper.SeedBasicData(seed);
            seed.AuditLogs.AddRange(
                NewLog(1, start.AddDays(1)),
                NewLog(2, end.AddDays(-1)),
                NewLog(3, end.AddDays(2)));
            seed.SaveChanges();
        }

        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.txt");
        try
        {
            var service = new AuditLogService(() => DatabaseHelper.CreateContext(connection));

            var manifest = service.ArchiveLogs(
                start,
                end,
                actorId: 1,
                path,
                (logs, outputPath) => File.WriteAllText(
                    outputPath,
                    string.Join(Environment.NewLine, logs.Select(log => log.Id))));

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
            Assert.Equal(3, Assert.Single(assertContext.AuditLogs).EntityId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static AuditLog NewLog(int entityId, DateTime performedAt) => new()
    {
        EntityName = "Product",
        EntityId = entityId,
        ActionCode = "UPDATE",
        PerformedBy = 1,
        PerformedAt = performedAt
    };
}