using System.IO;
using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Startup;

namespace QuanLyHangHoa.Tests.Startup;

public class StartupCoordinatorTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public async Task RunAsync_executes_dependencies_in_the_required_order()
    {
        var runtime = new FakeStartupRuntime();

        var result = await new StartupCoordinator(runtime).RunAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Null(result.ErrorCode);
        Assert.Equal(["load-config", "resolve-connection", "probe-sql", "initialize-database"], runtime.Calls);
    }

    [Theory]
    [InlineData("CFG-CONFIG-INVALID", "Kiểm tra lại cấu hình")]
    [InlineData("CFG-CREDENTIAL-MISSING", "Lưu lại tài khoản SQL")]
    [InlineData("SQL-SERVICE-UNAVAILABLE", "Khởi động dịch vụ SQL Server")]
    [InlineData("SQL-CREDENTIAL-REJECTED", "Kiểm tra tài khoản SQL")]
    [InlineData("DB-BACKUP-FAILED", "Kiểm tra quyền backup")]
    [InlineData("DB-SEED-MISSING", "Cài lại dữ liệu mẫu")]
    [InlineData("DB-CLIENT-UPDATE-REQUIRED", "Cập nhật WarePro")]
    public async Task RunAsync_returns_actionable_failure_codes(string code, string userMessage)
    {
        var runtime = new FakeStartupRuntime
        {
            Failure = new StartupFailureException(
                code,
                userMessage,
                "Server=db;User ID=admin;Password=p@ss;token=abc123")
        };

        var result = await new StartupCoordinator(runtime).RunAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(code, result.ErrorCode);
        Assert.Contains(userMessage, result.UserMessage, StringComparison.Ordinal);
        Assert.Equal(runtime.LogPath, result.LogPath);
        Assert.DoesNotContain("admin", result.TechnicalDetailRedacted, StringComparison.Ordinal);
        Assert.DoesNotContain("p@ss", result.TechnicalDetailRedacted, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", result.TechnicalDetailRedacted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_maps_seed_and_backup_failures_to_database_actions()
    {
        var seedResult = await new StartupCoordinator(new FakeStartupRuntime
        {
            Failure = new SeedWorkbookMissingException(@"C:\Program Files\WarePro\Database\seed.xlsx")
        }).RunAsync(CancellationToken.None);
        var backupResult = await new StartupCoordinator(new FakeStartupRuntime
        {
            Failure = new DatabaseBackupException(
                @"D:\SqlBackups\warepro.bak",
                new InvalidOperationException("verify failed"))
        }).RunAsync(CancellationToken.None);

        Assert.Equal("DB-SEED-MISSING", seedResult.ErrorCode);
        Assert.Contains("Cài lại dữ liệu mẫu", seedResult.UserMessage, StringComparison.Ordinal);
        Assert.Equal("DB-BACKUP-FAILED", backupResult.ErrorCode);
        Assert.Contains("quyền backup", backupResult.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_cancellation_stops_before_database_initialization()
    {
        using var cancellation = new CancellationTokenSource();
        var runtime = new FakeStartupRuntime
        {
            Probe = async token =>
            {
                cancellation.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        };

        var result = await new StartupCoordinator(runtime).RunAsync(cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal("INST-STARTUP-CANCELLED", result.ErrorCode);
        Assert.DoesNotContain("initialize-database", runtime.Calls);
    }

    [Fact]
    public void App_opens_login_only_after_the_coordinator_succeeds()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "App.xaml.cs"));

        var runIndex = source.IndexOf("await coordinator.RunAsync", StringComparison.Ordinal);
        var loginIndex = source.IndexOf("new LoginView()", StringComparison.Ordinal);
        var showIndex = source.IndexOf("login.Show()", StringComparison.Ordinal);

        Assert.True(runIndex >= 0, "Startup coordinator is not called.");
        Assert.True(loginIndex > runIndex, "LoginView is created before startup completes.");
        Assert.True(showIndex > loginIndex, "LoginView is not shown after successful startup.");
    }

    [Fact]
    public void Login_command_remains_disabled_until_database_ready()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "ViewModels", "LoginViewModel.cs"));

        Assert.Contains("[RelayCommand(CanExecute = nameof(CanLogin))]", source, StringComparison.Ordinal);
        Assert.Contains("private bool CanLogin() => IsDatabaseReady;", source, StringComparison.Ordinal);
    }

    private sealed class FakeStartupRuntime : IStartupRuntime
    {
        public List<string> Calls { get; } = [];
        public string LogPath { get; } = Path.Combine(Path.GetTempPath(), "WarePro", "Logs", "startup.log");
        public Exception? Failure { get; init; }
        public Func<CancellationToken, Task>? Probe { get; init; }

        public WareProSettings? LoadSettings()
        {
            Calls.Add("load-config");
            ThrowIfNeeded();
            return WareProSettings.CreateDefault();
        }

        public string ResolveConnectionString(WareProSettings? settings)
        {
            Calls.Add("resolve-connection");
            ThrowIfNeeded();
            return "Server=fake;Database=WarePro;Trusted_Connection=True";
        }

        public async Task ProbeSqlAsync(string connectionString, CancellationToken cancellationToken)
        {
            Calls.Add("probe-sql");
            ThrowIfNeeded();
            if (Probe is not null)
            {
                await Probe(cancellationToken);
            }
        }

        public Task InitializeDatabaseAsync(string connectionString, CancellationToken cancellationToken)
        {
            Calls.Add("initialize-database");
            ThrowIfNeeded();
            return Task.CompletedTask;
        }

        public string GetLogPath() => LogPath;

        private void ThrowIfNeeded()
        {
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }
}
