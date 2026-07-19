using Microsoft.Data.SqlClient;
using QuanLyHangHoa.Data;

namespace QuanLyHangHoa.Tests.Infrastructure;

/// <summary>
/// tạo một database SQL Server dùng riêng cho một lần test rồi xóa khi test kết thúc.
/// lớp này chỉ nhận connection string trỏ tới master để không thể xóa nhầm database nghiệp vụ.
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncDisposable
{
    public const string ConnectionStringEnvironmentVariable =
        "WAREPRO_SQLSERVER_TEST_CONNECTION_STRING";

    private const string DatabaseNamePrefix = "WarePro_ConcurrencyTest_";

    private readonly string _masterConnectionString;
    private readonly string _databaseConnectionString;
    private bool _disposed;

    private SqlServerTestDatabase(
        string masterConnectionString,
        string databaseConnectionString,
        string databaseName)
    {
        _masterConnectionString = masterConnectionString;
        _databaseConnectionString = databaseConnectionString;
        DatabaseName = databaseName;
    }

    public string DatabaseName { get; }

    public static SqlServerTestDatabase FromEnvironment()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable {ConnectionStringEnvironmentVariable} is not set.");
        }

        var masterBuilder = new SqlConnectionStringBuilder(rawConnectionString);
        if (!string.Equals(masterBuilder.InitialCatalog, "master", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringEnvironmentVariable} must target the SQL Server master database.");
        }

        // database test luôn có tên do code sinh ra. timestamp giúp đọc log, guid ngăn trùng khi ci chạy song song.
        var databaseName = DatabaseNamePrefix
            + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")
            + "_"
            + Guid.NewGuid().ToString("N")[..12];

        // tắt pooling vì một connection còn nằm trong pool sẽ cản lệnh drop database trên windows.
        masterBuilder.ApplicationName = "WarePro.SqlServerConcurrencyTests";
        masterBuilder.Pooling = false;

        var databaseBuilder = new SqlConnectionStringBuilder(masterBuilder.ConnectionString)
        {
            InitialCatalog = databaseName
        };

        return new SqlServerTestDatabase(
            masterBuilder.ConnectionString,
            databaseBuilder.ConnectionString,
            databaseName);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(@databaseName) IS NULL
            BEGIN
                DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
                EXEC sys.sp_executesql @sql;
            END
            """;
        command.Parameters.AddWithValue("@databaseName", DatabaseName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public AppDbContext CreateContext()
    {
        ThrowIfDisposed();

        return new AppDbContext(AppDbContextOptionsFactory.Create(_databaseConnectionString));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // guard này chạy trước khi mở connection. tên sai thì test dừng và không gửi lệnh drop.
        if (!DatabaseName.StartsWith(DatabaseNamePrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to drop an unguarded database.");
        }

        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{DatabaseName}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{DatabaseName}];
            END
            """;
        await command.ExecuteNonQueryAsync();

        // chỉ khóa object sau khi lệnh drop thành công; nếu sql lỗi tạm thời, caller vẫn có thể gọi cleanup lại.
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}