using System;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Services;

/// <summary>
/// tách lệnh SQL Server khỏi quy tắc đặt tên và xác minh backup.
/// </summary>
public interface IDatabaseBackupExecutor
{
    string GetDefaultBackupDirectory();
    void BackupWithChecksum(string databaseName, string backupPath);
    void VerifyWithChecksum(string backupPath);
}

public sealed record DatabaseBackupResult(string BackupPath, bool ChecksumVerified);

/// <summary>
/// giữ đường dẫn backup cuối cùng để chẩn đoán cả lỗi trước và sau khi lấy thư mục mặc định.
/// </summary>
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

/// <summary>
/// tạo bản backup trước nâng schema và chỉ trả thành công sau RESTORE VERIFYONLY WITH CHECKSUM.
/// </summary>
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

        // tên file mang phiên bản ứng dụng và UTC để truy ra lần nâng cấp đã tạo backup.
        var timestamp = _utcNowProvider().ToUniversalTime();
        var version = SanitizeFileNamePart(_appVersionProvider());
        var fileName = $"{databaseName}_before_warepro_{version}_{timestamp:yyyyMMdd'T'HHmmss'Z'}.bak";
        var backupPath = fileName;

        try
        {
            // backup và verify là một đơn vị kết quả; thiếu bước verify vẫn bị coi là thất bại.
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

    // phiên bản assembly có thể chứa ký tự không hợp lệ với tên file trên Windows.
    private static string SanitizeFileNamePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(character =>
            invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }
}

/// <summary>
/// thực thi backup tại chính SQL Server vì đường dẫn .bak được hiểu trên máy chủ SQL.
/// </summary>
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
        // ưu tiên thư mục backup của instance; nếu chưa cấu hình thì dùng thư mục chứa SQL error log.
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
        // COPY_ONLY không làm lệch chuỗi backup; INIT ghi mới file đích; CHECKSUM tạo dữ liệu để verify.
        command.CommandText = $"BACKUP DATABASE {QuoteIdentifier(databaseName)} TO DISK = @path WITH COPY_ONLY, INIT, CHECKSUM;";
        command.Parameters.AddWithValue("@path", backupPath);
        command.CommandTimeout = 0;
        command.ExecuteNonQuery();
    }

    public void VerifyWithChecksum(string backupPath)
    {
        using var command = _connection.CreateCommand();
        // VERIFYONLY kiểm tra bộ backup có thể đọc cùng checksum mà không restore vào database thật.
        command.CommandText = "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
        command.Parameters.AddWithValue("@path", backupPath);
        command.CommandTimeout = 0;
        command.ExecuteNonQuery();
    }

    private static string QuoteIdentifier(string value) => $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
}
