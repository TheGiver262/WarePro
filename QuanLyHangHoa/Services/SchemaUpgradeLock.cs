using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Services;

/// <summary>
/// báo mã trả về âm từ sp_getapplock để startup phân loại lỗi chờ khóa schema.
/// </summary>
public sealed class SchemaUpgradeLockException : Exception
{
    public SchemaUpgradeLockException(int resultCode)
        : base($"DB-SCHEMA-LOCK-TIMEOUT: SQL application lock failed with code {resultCode}.")
    {
        Code = "DB-SCHEMA-LOCK-TIMEOUT";
        ResultCode = resultCode;
    }

    public string Code { get; }
    public int ResultCode { get; }
}

/// <summary>
/// khóa nâng cấp theo tên database để chỉ một tiến trình được tạo, backup, migrate và seed cùng lúc.
/// </summary>
public sealed class SchemaUpgradeLock : IDisposable
{
    // khóa thuộc session nên tồn tại qua nhiều transaction và tự mất nếu connection bị đóng.
    private const string AcquireSql = """
        DECLARE @result INT;
        DECLARE @resource NVARCHAR(255) = CONCAT(N'WarePro.SchemaUpgrade:', @databaseName);
        EXEC @result = sys.sp_getapplock
            @Resource = @resource,
            @LockMode = N'Exclusive',
            @LockOwner = N'Session',
            @LockTimeout = @lockTimeout;
        SELECT @result;
        """;

    private const string ReleaseSql = """
        DECLARE @resource NVARCHAR(255) = CONCAT(N'WarePro.SchemaUpgrade:', @databaseName);
        EXEC sys.sp_releaseapplock
            @Resource = @resource,
            @LockOwner = N'Session';
        """;

    private readonly SqlConnection _connection;
    private readonly string _databaseName;
    private bool _disposed;

    private SchemaUpgradeLock(SqlConnection connection, string databaseName)
    {
        _connection = connection;
        _databaseName = databaseName;
    }

    public static SchemaUpgradeLock Acquire(
        SqlConnection connection,
        string databaseName,
        int timeoutMilliseconds = 30000)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("SQL connection must be open before acquiring the schema lock.");
        }

        // connection phải mở tới master để khóa tồn tại ngay cả trước khi database đích được tạo.
        using var command = connection.CreateCommand();
        command.CommandText = AcquireSql;
        command.Parameters.AddWithValue("@databaseName", databaseName);
        command.Parameters.AddWithValue("@lockTimeout", timeoutMilliseconds);
        // sp_getapplock trả số không âm khi lấy được khóa và số âm cho timeout, hủy hoặc lỗi.
        var result = Convert.ToInt32(command.ExecuteScalar());
        if (result < 0)
        {
            throw new SchemaUpgradeLockException(result);
        }

        return new SchemaUpgradeLock(connection, databaseName);
    }

    public void Dispose()
    {
        // Dispose có thể gọi lặp mà không gửi thêm lệnh release cho cùng session.
        if (_disposed)
        {
            return;
        }

        // đánh dấu trước khi gọi SQL để lần dispose sau không cố release lại nếu lệnh này ném lỗi.
        _disposed = true;
        using var command = _connection.CreateCommand();
        command.CommandText = ReleaseSql;
        command.Parameters.AddWithValue("@databaseName", _databaseName);
        command.ExecuteNonQuery();
    }
}
