using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Startup;

/// <summary>
/// tách các thao tác phụ thuộc file và SQL khỏi coordinator để kiểm thử được thứ tự khởi động.
/// </summary>
public interface IStartupRuntime
{
    WareProSettings? LoadSettings();
    string ResolveConnectionString(WareProSettings? settings);
    Task ProbeSqlAsync(string connectionString, CancellationToken cancellationToken);
    Task InitializeDatabaseAsync(string connectionString, WareProSettings? settings, CancellationToken cancellationToken);
    string GetLogPath();
}

/// <summary>
/// điều phối toàn bộ điều kiện bắt buộc trước khi giao diện được phép dùng cơ sở dữ liệu.
/// </summary>
public sealed class StartupCoordinator : IAsyncDisposable
{
    private readonly IStartupRuntime _runtime;

    public StartupCoordinator(IStartupRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public static StartupCoordinator CreateDefault() => new(new DefaultStartupRuntime());

    public async Task<StartupResult> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // thứ tự là bất biến: cấu hình quyết định kết nối, probe xác nhận SQL, rồi initializer mới nâng cấp dữ liệu.
            var settings = _runtime.LoadSettings();
            cancellationToken.ThrowIfCancellationRequested();
            var connectionString = _runtime.ResolveConnectionString(settings);
            await _runtime.ProbeSqlAsync(connectionString, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // startup kiểm tra trạng thái tương thích, không tự migrate để một client mở app không đổi database dùng chung.
            await _runtime.InitializeDatabaseAsync(connectionString, settings, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return StartupResult.Succeeded(GetLogPathSafely());
        }
        // cancellation do caller yêu cầu có mã riêng, không bị ghi nhận nhầm thành lỗi hệ thống.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                "INST-STARTUP-CANCELLED",
                "Khởi động đã bị hủy. Mở lại WarePro để thử lại.",
                "Startup was cancelled.");
        }
        // các exception có kiểu riêng giữ mã lỗi và hướng xử lý phù hợp cho từng nguyên nhân.
        catch (StartupFailureException ex)
        {
            return Failure(ex.Code, ex.UserMessage, ex.ToString());
        }
        catch (WareProConfigurationException ex)
        {
            return Failure(
                ex.Code,
                "Kiểm tra lại cấu hình WarePro trong ProgramData rồi mở lại ứng dụng.",
                ex.ToString());
        }
        catch (WareProCredentialException ex)
        {
            return Failure(
                ex.Code,
                "Lưu lại tài khoản SQL trong Windows Credential Manager rồi thử lại.",
                ex.ToString());
        }
        catch (DatabaseCompatibilityException ex)
        {
            return Failure(
                ex.Code,
                "Cập nhật WarePro lên phiên bản mới trước khi đăng nhập.",
                ex.ToString());
        }
        catch (SchemaUpgradeLockException ex)
        {
            return Failure(
                ex.Code,
                "Đóng WarePro trên máy khác, chờ một lúc rồi thử lại.",
                ex.ToString());
        }
        catch (SeedWorkbookMissingException ex)
        {
            return Failure(
                ex.Code,
                "Cài lại dữ liệu mẫu WarePro rồi mở lại ứng dụng.",
                ex.ToString());
        }
        catch (DatabaseBackupException ex)
        {
            return Failure(
                ex.Code,
                "Kiểm tra quyền backup và dung lượng ổ đĩa SQL Server rồi thử lại.",
                ex.ToString());
        }
        catch (SqlException ex)
        {
            return Failure(
                "DB-UPGRADE-FAILED",
                "Kiểm tra quyền cập nhật database hoặc liên hệ quản trị viên SQL.",
                ex.ToString());
        }
        // lỗi ngoài dự kiến được gom về mã chung; chi tiết vẫn được lọc trước khi rời coordinator.
        catch (Exception ex)
        {
            return Failure(
                "INST-STARTUP-FAILED",
                "WarePro chưa thể khởi động. Kiểm tra log rồi thử lại.",
                ex.ToString());
        }
    }

    // userMessage có thể hiển thị; technicalDetail chỉ dùng chẩn đoán và luôn qua bộ lọc credential.
    private StartupResult Failure(string code, string userMessage, string technicalDetail) =>
        StartupResult.Failed(
            code,
            userMessage,
            SensitiveDataRedactor.Redact(technicalDetail),
            GetLogPathSafely());

    // lỗi khi tính đường dẫn log không được che kết quả startup ban đầu.
    public async ValueTask DisposeAsync()
    {
        if (_runtime is IAsyncDisposable disposable)
            await disposable.DisposeAsync().ConfigureAwait(false);
    }

    private string GetLogPathSafely()
    {
        try
        {
            return _runtime.GetLogPath();
        }
        catch
        {
            return Path.Combine(Path.GetTempPath(), "WarePro", "Logs");
        }
    }
}

/// <summary>
/// runtime thật nối coordinator với cấu hình máy, SqlClient và bộ khởi tạo cơ sở dữ liệu.
/// </summary>
public sealed class DefaultStartupRuntime : IStartupRuntime, IAsyncDisposable
{
    private ClientSessionLease? _sessionLease;
    public WareProSettings? LoadSettings() => new WareProSettingsStore().Load();

    public string ResolveConnectionString(WareProSettings? settings) =>
        ConnectionStringFactory.CreateDefault().Resolve(settings);

    public async Task ProbeSqlAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
        }
        // mã SQL 18456 xác nhận credential bị từ chối và cho phép App hỏi lại đúng một lần.
        catch (SqlException ex) when (ex.Number == 18456)
        {
            throw new StartupFailureException(
                "SQL-CREDENTIAL-REJECTED",
                "Kiểm tra tài khoản SQL rồi thử kết nối lại.",
                ex.Message,
                ex);
        }
        catch (SqlException ex)
        {
            throw new StartupFailureException(
                "SQL-SERVICE-UNAVAILABLE",
                "Khởi động dịch vụ SQL Server và kiểm tra tên máy chủ.",
                ex.Message,
                ex);
        }
    }

    public async Task InitializeDatabaseAsync(
        string connectionString,
        WareProSettings? settings,
        CancellationToken cancellationToken)
    {
        var readiness = new DatabaseReadinessService(ReadDatabaseSnapshotAsync);
        var result = await readiness.CheckDatabaseReadyAsync(
            connectionString,
            GetAppVersion(),
            cancellationToken);
        if (result.Status == DatabaseReadinessStatus.UpgradeRequired)
            throw new StartupFailureException("DB-UPGRADE-REQUIRED", "Hãy chạy bộ cài để nâng database.", result.Code);
        if (result.Status == DatabaseReadinessStatus.ClientUpdateRequired)
            throw new StartupFailureException("DB-CLIENT-UPDATE-REQUIRED", "Client hiện tại không tương thích database.", result.Code);
        if (result.Status == DatabaseReadinessStatus.Unavailable)
            throw new StartupFailureException("DB-UNAVAILABLE", "Không đọc được trạng thái database.", result.Code);

        // runtime sở hữu lease này đến DisposeAsync; nhờ vậy session không biến mất khi coordinator còn phục vụ app.
        await FirstInstallDemoSeeder.CreateDefault(connectionString, settings).RunAsync(cancellationToken);

        _sessionLease = await ClientSessionLease.RegisterAsync(
            connectionString,
            GetAppVersion().ToString(3),
            cancellationToken);
    }
    private static async Task<DatabaseReadinessSnapshot> ReadDatabaseSnapshotAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @SchemaVersion INT = 0;
            DECLARE @MinimumClientVersion NVARCHAR(32) = N'1.0.0';
            IF OBJECT_ID(N'dbo.__WareProSchemaVersion', N'U') IS NOT NULL
            BEGIN
                EXEC sys.sp_executesql
                    N'SELECT @value = ISNULL(MAX([Version]), 0) FROM dbo.__WareProSchemaVersion;',
                    N'@value INT OUTPUT',
                    @value = @SchemaVersion OUTPUT;
                IF COL_LENGTH(N'dbo.__WareProSchemaVersion', N'MinimumClientVersion') IS NOT NULL
                    EXEC sys.sp_executesql
                        N'SELECT @value = COALESCE(MAX(NULLIF([MinimumClientVersion], N'''')), N''1.0.0'') FROM dbo.__WareProSchemaVersion;',
                        N'@value NVARCHAR(32) OUTPUT',
                        @value = @MinimumClientVersion OUTPUT;
            END;
            SELECT @SchemaVersion, @MinimumClientVersion;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new DatabaseReadinessSnapshot(0, "1.0.0", 0);
        return new DatabaseReadinessSnapshot(reader.GetInt32(0), reader.GetString(1), 0);
    }
    private static Version GetAppVersion() =>
        typeof(StartupCoordinator).Assembly.GetName().Version ?? new Version("0.0.0");
    public string GetLogPath()
    {
        var directory = WareProPaths.Current.UserLogDirectory;
        return Path.Combine(directory, $"crash-{DateTimeOffset.UtcNow:yyyyMMdd}.log");
    }
    public async ValueTask DisposeAsync()
    {
        if (_sessionLease is not null)
        {
            // giải phóng lease lúc app tắt để client khác không phải chờ heartbeat hết hạn.
            await _sessionLease.DisposeAsync().ConfigureAwait(false);
            _sessionLease = null;
        }
    }
}
