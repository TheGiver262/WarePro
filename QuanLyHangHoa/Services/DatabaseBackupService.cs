using System;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Services;

public interface IDatabaseBackupExecutor
{
    string GetDefaultBackupDirectory();
    void BackupWithChecksum(string databaseName, string backupPath);
    void VerifyWithChecksum(string backupPath);
}

public sealed record DatabaseBackupResult(string BackupPath, bool ChecksumVerified);

public sealed class DatabaseBackupException : InvalidOperationException
{
    public DatabaseBackupException(string backupPath, Exception innerException)
        : base($"DB-BACKUP-FAILED: Database backup could not be created and verified: {backupPath}", innerException)
    {
        Code = "DB-BACKUP-FAILED";
        BackupPath = backupPath;
    }

    public string Code { get; }
    public string BackupPath { get; }
}

public sealed class DatabaseBackupService
{
    private readonly IDatabaseBackupExecutor _executor;
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private readonly Func<string> _appVersionProvider;

    public DatabaseBackupService(
        IDatabaseBackupExecutor executor,
        Func<DateTimeOffset> utcNowProvider,
        Func<string> appVersionProvider)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
        _appVersionProvider = appVersionProvider ?? throw new ArgumentNullException(nameof(appVersionProvider));
    }

    public DatabaseBackupResult CreateAndVerify(string databaseName)
    {
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new ArgumentException("Database name cannot be empty.", nameof(databaseName));
        }

        var timestamp = _utcNowProvider().ToUniversalTime();
        var version = SanitizeFileNamePart(_appVersionProvider());
        var fileName = $"{databaseName}_before_warepro_{version}_{timestamp:yyyyMMdd'T'HHmmss'Z'}.bak";
        var backupPath = fileName;

        try
        {
            backupPath = Path.Combine(_executor.GetDefaultBackupDirectory(), fileName);
            _executor.BackupWithChecksum(databaseName, backupPath);
            _executor.VerifyWithChecksum(backupPath);
            return new DatabaseBackupResult(backupPath, ChecksumVerified: true);
        }
        catch (Exception ex) when (ex is not DatabaseBackupException)
        {
            throw new DatabaseBackupException(backupPath, ex);
        }
    }

    private static string SanitizeFileNamePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character =>
            invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }
}

public sealed class SqlDatabaseBackupExecutor : IDatabaseBackupExecutor
{
    private readonly SqlConnection _connection;

    public SqlDatabaseBackupExecutor(SqlConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public string GetDefaultBackupDirectory()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(
                CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(4000)),
                LEFT(
                    CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)),
                    LEN(CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)))
                    - CHARINDEX('\', REVERSE(CAST(SERVERPROPERTY('ErrorLogFileName') AS NVARCHAR(4000)))))
            );
            """;
        return Convert.ToString(command.ExecuteScalar())
            ?? throw new InvalidOperationException("SQL Server did not return a backup directory.");
    }

    public void BackupWithChecksum(string databaseName, string backupPath)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"BACKUP DATABASE {QuoteIdentifier(databaseName)} TO DISK = @path WITH COPY_ONLY, INIT, CHECKSUM;";
        command.Parameters.AddWithValue("@path", backupPath);
        command.CommandTimeout = 0;
        command.ExecuteNonQuery();
    }

    public void VerifyWithChecksum(string backupPath)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
        command.Parameters.AddWithValue("@path", backupPath);
        command.CommandTimeout = 0;
        command.ExecuteNonQuery();
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
