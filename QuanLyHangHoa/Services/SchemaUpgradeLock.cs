using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Services;

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

public sealed class SchemaUpgradeLock : IDisposable
{
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

        using var command = connection.CreateCommand();
        command.CommandText = AcquireSql;
        command.Parameters.AddWithValue("@databaseName", databaseName);
        command.Parameters.AddWithValue("@lockTimeout", timeoutMilliseconds);
        var result = Convert.ToInt32(command.ExecuteScalar());
        if (result < 0)
        {
            throw new SchemaUpgradeLockException(result);
        }

        return new SchemaUpgradeLock(connection, databaseName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        using var command = _connection.CreateCommand();
        command.CommandText = ReleaseSql;
        command.Parameters.AddWithValue("@databaseName", _databaseName);
        command.ExecuteNonQuery();
    }
}
