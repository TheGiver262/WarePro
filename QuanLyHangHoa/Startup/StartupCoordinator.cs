using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Data;
using QuanLyHangHoa.Services;

namespace QuanLyHangHoa.Startup;

public interface IStartupRuntime
{
    WareProSettings? LoadSettings();
    string ResolveConnectionString(WareProSettings? settings);
    Task ProbeSqlAsync(string connectionString, CancellationToken cancellationToken);
    Task InitializeDatabaseAsync(string connectionString, CancellationToken cancellationToken);
    string GetLogPath();
}

public sealed class StartupCoordinator
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
            var settings = _runtime.LoadSettings();
            cancellationToken.ThrowIfCancellationRequested();
            var connectionString = _runtime.ResolveConnectionString(settings);
            await _runtime.ProbeSqlAsync(connectionString, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await _runtime.InitializeDatabaseAsync(connectionString, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return StartupResult.Succeeded(GetLogPathSafely());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                "INST-STARTUP-CANCELLED",
                "Khởi động đã bị hủy. Mở lại WarePro để thử lại.",
                "Startup was cancelled.");
        }
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
        catch (Exception ex)
        {
            return Failure(
                "INST-STARTUP-FAILED",
                "WarePro chưa thể khởi động. Kiểm tra log rồi thử lại.",
                ex.ToString());
        }
    }

    private StartupResult Failure(string code, string userMessage, string technicalDetail) =>
        StartupResult.Failed(
            code,
            userMessage,
            SensitiveDataRedactor.Redact(technicalDetail),
            GetLogPathSafely());

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

public sealed class DefaultStartupRuntime : IStartupRuntime
{
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

    public Task InitializeDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(connectionString)
                .Options;
            var initializer = new DatabaseInitializer(
                () => new AppDbContext(options),
                WareProPaths.Current.InstallDirectory,
                connectionString);
            initializer.Initialize(cancellationToken);
        }, cancellationToken);
    }

    public string GetLogPath()
    {
        var directory = WareProPaths.Current.UserLogDirectory;
        return Path.Combine(directory, $"crash-{DateTimeOffset.UtcNow:yyyyMMdd}.log");
    }
}
