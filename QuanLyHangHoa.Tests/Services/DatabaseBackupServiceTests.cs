using System.IO;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Tests.Services;

public class DatabaseBackupServiceTests
{
    [Fact]
    public void CreateAndVerify_uses_the_server_backup_directory_and_verifies_checksum()
    {
        var executor = new FakeBackupExecutor(@"D:\SqlBackups");
        var service = new DatabaseBackupService(
            executor,
            () => new DateTimeOffset(2026, 7, 16, 3, 4, 5, TimeSpan.Zero),
            () => "1.2.3");

        var result = service.CreateAndVerify("ProductManagementDb");

        Assert.Equal(
            Path.Combine(
                @"D:\SqlBackups",
                "ProductManagementDb_before_warepro_1.2.3_20260716T030405Z.bak"),
            result.BackupPath);
        Assert.True(result.ChecksumVerified);
        Assert.Equal(["backup", "verify"], executor.Calls);
        Assert.Equal(result.BackupPath, executor.BackupPath);
        Assert.Equal(result.BackupPath, executor.VerifyPath);
    }

    [Fact]
    public void CreateAndVerify_does_not_report_success_when_verify_fails()
    {
        var executor = new FakeBackupExecutor(@"D:\SqlBackups") { FailVerify = true };
        var service = new DatabaseBackupService(
            executor,
            () => new DateTimeOffset(2026, 7, 16, 3, 4, 5, TimeSpan.Zero),
            () => "1.2.3");

        var error = Assert.Throws<DatabaseBackupException>(
            () => service.CreateAndVerify("ProductManagementDb"));
        Assert.Equal("DB-BACKUP-FAILED", error.Code);
        Assert.EndsWith(".bak", error.BackupPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["backup", "verify"], executor.Calls);
    }

    private sealed class FakeBackupExecutor(string backupDirectory) : IDatabaseBackupExecutor
    {
        public List<string> Calls { get; } = [];
        public bool FailVerify { get; init; }
        public string? BackupPath { get; private set; }
        public string? VerifyPath { get; private set; }

        public string GetDefaultBackupDirectory() => backupDirectory;

        public void BackupWithChecksum(string databaseName, string backupPath)
        {
            Calls.Add("backup");
            BackupPath = backupPath;
        }

        public void VerifyWithChecksum(string backupPath)
        {
            Calls.Add("verify");
            VerifyPath = backupPath;
            if (FailVerify)
            {
                throw new InvalidOperationException("verify failed");
            }
        }
    }
}
