using System.Text.Json;
using QuanLyHangHoa.Configuration;
using WarePro.SetupHelper;

namespace QuanLyHangHoa.Tests.Deployment;

public class SetupHelperTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Theory]
    [InlineData("unknown")]
    [InlineData("write-config", "--password", "secret")]
    [InlineData("test-connection", "--connection-string", "Server=.;Password=secret")]
    public async Task Unknown_command_or_option_is_rejected(params string[] arguments)
    {
        var commands = CreateCommands();

        var result = await commands.ExecuteAsync(arguments);

        Assert.Equal(SetupExitCode.InvalidArguments, result.ExitCode);
        Assert.DoesNotContain("secret", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Program_logs_backup_database_command()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "WarePro.SetupHelper", "Program.cs"));

        Assert.Contains(@"""backup-database"" => ""backup-database""", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detect_sql_returns_probe_exit_code_and_machine_readable_summary()
    {
        var probe = new FakeProbe
        {
            DetectResult = new SetupProbeResult(
                SetupExitCode.SqlInstanceNotFound,
                "SQL instance not found.",
                @".\SQLEXPRESS")
        };
        var commands = CreateCommands(probe);

        var result = await commands.ExecuteAsync(
            ["detect-sql", "--instance", @".\SQLEXPRESS"]);

        Assert.Equal(SetupExitCode.SqlInstanceNotFound, result.ExitCode);
        Assert.Contains("\"code\":20", result.Summary, StringComparison.Ordinal);
        Assert.Contains("SQLEXPRESS", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detect_sql_maps_unexpected_probe_failure_to_service_unavailable()
    {
        var probe = new FakeProbe { DetectException = new InvalidOperationException("service query failed") };
        var commands = CreateCommands(probe);

        var result = await commands.ExecuteAsync(
            ["detect-sql", "--instance", @".\SQLEXPRESS"]);

        Assert.Equal(SetupExitCode.SqlServiceUnavailable, result.ExitCode);
    }

    [Fact]
    public async Task Write_config_preserves_paths_with_spaces_and_validated_values()
    {
        var writer = new FakeWriter();
        var commands = CreateCommands(writer: writer);
        var path = Path.Combine(Path.GetTempPath(), "Ware Pro Data", "appsettings.json");

        var result = await commands.ExecuteAsync(
        [
            "write-config",
            "--server", @".\SQLEXPRESS",
            "--database", "ProductManagementDb",
            "--auth", "Windows",
            "--encrypt", "false",
            "--config", path
        ]);

        Assert.Equal(SetupExitCode.Success, result.ExitCode);
        Assert.Equal(Path.GetFullPath(path), writer.Path);
        Assert.Equal(@".\SQLEXPRESS", writer.Settings?.Database.Server);
        Assert.Equal(DatabaseAuthentication.Windows, writer.Settings?.Database.Authentication);
        Assert.False(writer.Settings?.Database.Encrypt);
    }

    [Fact]
    public async Task Write_config_persists_client_role_without_storing_credentials()
    {
        var writer = new FakeWriter();
        var commands = CreateCommands(writer: writer);

        var result = await commands.ExecuteAsync(
        [
            "write-config",
            "--server", "tcp:192.168.10.10,1433",
            "--database", "ProductManagementDb",
            "--auth", "SqlPassword",
            "--role", "Client",
            "--initial-data", "None"
        ]);

        var json = WareProSettingsStore.Serialize(writer.Settings!);
        using var document = JsonDocument.Parse(json);
        var database = document.RootElement.GetProperty("Database");

        Assert.Equal(SetupExitCode.Success, result.ExitCode);
        Assert.Equal(DeploymentRole.Client, writer.Settings!.DeploymentRole);
        Assert.Equal(InitialDataProfile.None, writer.Settings.InitialDataProfile);
        Assert.False(database.TryGetProperty("Password", out _));
        Assert.False(database.TryGetProperty("Pwd", out _));
        Assert.False(database.TryGetProperty("User ID", out _));
        Assert.False(database.TryGetProperty("UserId", out _));
        Assert.False(database.TryGetProperty("Credential", out _));
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Client", "Demo")]
    [InlineData("unknown-role", "None")]
    public async Task Write_config_rejects_unsafe_or_unknown_deployment_values(
        string role,
        string initialData)
    {
        var commands = CreateCommands();

        var result = await commands.ExecuteAsync(
        [
            "write-config",
            "--server", "server",
            "--database", "database",
            "--auth", "Windows",
            "--role", role,
            "--initial-data", initialData
        ]);

        Assert.Equal(SetupExitCode.InvalidArguments, result.ExitCode);
    }

    [Fact]
    public async Task Backup_database_uses_only_the_saved_config_and_returns_the_verified_backup()
    {
        var probe = new FakeProbe
        {
            BackupResult = new SetupProbeResult(
                SetupExitCode.Success,
                "Database backup was verified.",
                @"D:\SqlBackups\WarePro.bak")
        };
        var commands = CreateCommands(probe);
        var path = Path.Combine(Path.GetTempPath(), "Ware Pro Data", "appsettings.json");

        var result = await commands.ExecuteAsync(["backup-database", "--config", path]);

        Assert.Equal(SetupExitCode.Success, result.ExitCode);
        Assert.Equal(Path.GetFullPath(path), probe.BackupConfigPath);
        Assert.Contains("verified", result.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("restore", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Configure_lan_forwards_only_the_static_local_subnet_endpoint()
    {
        var configurator = new FakeLanConfigurator();
        var commands = CreateCommands(lanConfigurator: configurator);

        var result = await commands.ExecuteAsync(
        [
            "configure-lan",
            "--instance", "SQLEXPRESS",
            "--port", "1433",
            "--scope", "LocalSubnet"
        ]);

        Assert.Equal(SetupExitCode.Success, result.ExitCode);
        Assert.Equal(new SqlLanOptions("SQLEXPRESS", 1433, "LocalSubnet"), configurator.Options);
    }
    [Fact]
    public async Task Write_config_failure_uses_stable_code_and_redacts_detail()
    {
        var writer = new FakeWriter
        {
            Exception = new IOException("Password=secret;User ID=sa; cannot write")
        };
        var commands = CreateCommands(writer: writer);

        var result = await commands.ExecuteAsync(
        [
            "write-config",
            "--server", "server",
            "--database", "database",
            "--auth", "SqlPassword"
        ]);

        Assert.Equal(SetupExitCode.ConfigWriteFailed, result.ExitCode);
        Assert.DoesNotContain("secret", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sa", result.TechnicalDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(SetupExitCode.Success)]
    [InlineData(SetupExitCode.ConnectionFailed)]
    [InlineData(SetupExitCode.InsufficientDatabasePermission)]
    public async Task Test_connection_forwards_stable_probe_result(SetupExitCode exitCode)
    {
        var probe = new FakeProbe
        {
            ConnectionResult = new SetupProbeResult(exitCode, "connection result")
        };
        var commands = CreateCommands(probe);
        var path = Path.Combine(Path.GetTempPath(), "Ware Pro Data", "appsettings.json");

        var result = await commands.ExecuteAsync(
            ["test-connection", "--config", path, "--mode", "app-only"]);

        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(Path.GetFullPath(path), probe.ConfigPath);
        Assert.Equal(SetupMode.AppOnly, probe.Mode);
    }

    [Theory]
    [InlineData(15, "Express Edition", false)]
    [InlineData(16, "Standard Edition", false)]
    [InlineData(16, "Express Edition (64-bit)", true)]
    [InlineData(17, "Express Edition", true)]
    public void Full_install_requires_SQL_Server_2022_or_newer_Express(
        int productMajorVersion,
        string edition,
        bool expected)
    {
        Assert.Equal(
            expected,
            SqlSetupProbe.IsSupportedFullInstall(productMajorVersion, edition));
    }

    [Fact]
    public void Detect_sql_reads_version_and_edition_from_the_running_instance()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "WarePro.SetupHelper", "SetupCommands.cs"));

        Assert.Contains("SERVERPROPERTY('ProductMajorVersion')", source, StringComparison.Ordinal);
        Assert.Contains("SERVERPROPERTY('Edition')", source, StringComparison.Ordinal);
        Assert.Contains("SqlVersionUnsupported", source, StringComparison.Ordinal);
        Assert.Equal(24, (int)SetupExitCode.SqlVersionUnsupported);
    }

    private static SetupCommands CreateCommands(
        FakeProbe? probe = null,
        FakeWriter? writer = null,
        FakeLanConfigurator? lanConfigurator = null) => new(
            probe ?? new FakeProbe(),
            writer ?? new FakeWriter(),
            () => Path.Combine(Path.GetTempPath(), "WarePro", "appsettings.json"),
            lanConfigurator ?? new FakeLanConfigurator());

    private sealed class FakeProbe : ISetupProbe
    {
        public SetupProbeResult DetectResult { get; init; } =
            new(SetupExitCode.Success, "SQL instance is ready.");
        public SetupProbeResult ConnectionResult { get; init; } =
            new(SetupExitCode.Success, "Database is ready.");
        public SetupProbeResult BackupResult { get; init; } =
            new(SetupExitCode.Success, "Database backup was verified.");
        public Exception? DetectException { get; init; }
        public string? ConfigPath { get; private set; }
        public string? BackupConfigPath { get; private set; }
        public SetupMode Mode { get; private set; }

        public Task<SetupProbeResult> DetectSqlAsync(
            string instance,
            CancellationToken cancellationToken)
        {
            if (DetectException is not null)
            {
                throw DetectException;
            }

            return Task.FromResult(DetectResult);
        }

        public Task<SetupProbeResult> BackupDatabaseAsync(
            string configPath,
            CancellationToken cancellationToken)
        {
            BackupConfigPath = configPath;
            return Task.FromResult(BackupResult);
        }

        public Task<SetupProbeResult> TestConnectionAsync(
            string configPath,
            SetupMode mode,
            CancellationToken cancellationToken)
        {
            ConfigPath = configPath;
            Mode = mode;
            return Task.FromResult(ConnectionResult);
        }
    }

    private sealed class FakeLanConfigurator : IServerNetworkConfigurator
    {
        public SqlLanOptions? Options { get; private set; }

        public Task ConfigureAsync(SqlLanOptions options, CancellationToken cancellationToken)
        {
            Options = options;
            return Task.CompletedTask;
        }
    }
    private sealed class FakeWriter : ISetupConfigWriter
    {
        public string? Path { get; private set; }
        public WareProSettings? Settings { get; private set; }
        public Exception? Exception { get; init; }

        public void Save(string path, WareProSettings settings)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            Path = path;
            Settings = settings;
        }
    }
}
