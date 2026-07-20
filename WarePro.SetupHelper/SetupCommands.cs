using System.Data;
using System.Data.Common;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyHangHoa.Configuration;
using WarePro.Database;

namespace WarePro.SetupHelper;

/// <summary>
/// mã tiến trình ổn định để script cài đặt phân biệt lỗi tham số, SQL, quyền và ghi cấu hình.
/// </summary>
public enum SetupExitCode
{
    Success = 0,
    InvalidArguments = 10,
    SqlInstanceNotFound = 20,
    SqlServiceUnavailable = 21,
    ConnectionFailed = 22,
    InsufficientDatabasePermission = 23,
    SqlVersionUnsupported = 24,
    ActiveClients = 25,
    BackupFailed = 26,
    MigrationFailed = 27,
    ValidationFailed = 28,
    ConfigWriteFailed = 30
}

/// <summary>
/// full có thể chuẩn bị database mới; app-only chỉ kết nối database đã tồn tại.
/// </summary>
public enum SetupMode
{
    Full,
    AppOnly
}

public sealed record SetupProbeResult(
    SetupExitCode ExitCode,
    string Message,
    string? Target = null,
    string? TechnicalDetail = null);

public sealed record SetupExecutionResult(
    SetupExitCode ExitCode,
    string Summary,
    string TechnicalDetail = "");

/// <summary>
/// ranh giới kiểm tra SQL để command parser có thể kiểm thử không cần instance thật.
/// </summary>
public interface ISetupProbe
{
    Task<SetupProbeResult> DetectSqlAsync(
        string instance,
        CancellationToken cancellationToken);

    Task<SetupProbeResult> TestConnectionAsync(
        string configPath,
        SetupMode mode,
        CancellationToken cancellationToken);

    Task<SetupProbeResult> UpgradeDatabaseAsync(string configPath, Version appVersion, int expectedSchema, CancellationToken cancellationToken) =>
        Task.FromResult(new SetupProbeResult(SetupExitCode.MigrationFailed, "Database upgrade is not available."));
    Task<SetupProbeResult> PrepareDatabaseAsync(string configPath, Version appVersion, int expectedSchema, string? bootstrapSecretFile, CancellationToken cancellationToken) =>
        UpgradeDatabaseAsync(configPath, appVersion, expectedSchema, cancellationToken);
    Task<SetupProbeResult> FinalizeDatabaseAsync(string configPath, Version appVersion, int expectedSchema, CancellationToken cancellationToken) =>
        Task.FromResult(new SetupProbeResult(SetupExitCode.MigrationFailed, "Database finalize is not available."));
    Task<SetupProbeResult> RollbackDatabaseAsync(string configPath, Version appVersion, int expectedSchema, CancellationToken cancellationToken) =>
        Task.FromResult(new SetupProbeResult(SetupExitCode.MigrationFailed, "Database rollback is not available."));
}

public interface ISetupConfigWriter
{
    void Save(string path, WareProSettings settings);
}

/// <summary>
/// phân tích command của bộ cài, gọi probe hoặc ghi cấu hình và trả summary không chứa credential.
/// </summary>
public sealed class SetupCommands
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISetupProbe _probe;
    private readonly ISetupConfigWriter _writer;
    private readonly Func<string> _defaultConfigPath;

    public SetupCommands(
        ISetupProbe probe,
        ISetupConfigWriter writer,
        Func<string> defaultConfigPath)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _defaultConfigPath = defaultConfigPath ?? throw new ArgumentNullException(nameof(defaultConfigPath));
    }

    public static SetupCommands CreateDefault() => new(
        new SqlSetupProbe(),
        new SetupConfigWriter(),
        () => WareProPaths.Current.MachineConfigPath);

    public async Task<SetupExecutionResult> ExecuteAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (arguments.Count == 0)
        {
            return Invalid("A command is required.");
        }

        // command đầu tiên chọn đúng một thao tác; mỗi thao tác tự giới hạn tập option được phép.
        return arguments[0].ToLowerInvariant() switch
        {
            "detect-sql" => await DetectSqlAsync(arguments, cancellationToken),
            "write-config" => WriteConfig(arguments),
            "test-connection" => await TestConnectionAsync(arguments, cancellationToken),
            "upgrade-database" or "prepare-database" or "finalize-database" or "rollback-database" =>
                await DatabaseCutoverAsync(arguments, cancellationToken),
            _ => Invalid("Unknown command.")
        };
    }

    // hàm này chỉ lấy option phụ như --log; validation đầy đủ vẫn nằm trong TryParseOptions.
    public static string? FindOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 1; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private async Task<SetupExecutionResult> DetectSqlAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParseOptions(arguments, ["--instance", "--log"], out var options, out var error)
            || !Required(options, "--instance", out var instance))
        {
            return Invalid(error ?? "--instance is required.");
        }

        try
        {
            var result = await _probe.DetectSqlAsync(instance, cancellationToken);
            return FromProbe(result);
        }
        catch (Exception ex)
        {
            return Result(
                SetupExitCode.SqlServiceUnavailable,
                "SQL service is unavailable.",
                detail: ex.Message);
        }
    }

    private async Task<SetupExecutionResult> DatabaseCutoverAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParseOptions(
                arguments,
                ["--config", "--app-version", "--expected-schema", "--bootstrap-secret-file", "--log"],
                out var options,
                out var error)
            || !Required(options, "--config", out var configPath)
            || !Required(options, "--app-version", out var appVersionText)
            || !Required(options, "--expected-schema", out var schemaText)
            || !Version.TryParse(appVersionText, out var appVersion)
            || !int.TryParse(schemaText, out var expectedSchema)
            || expectedSchema < 1)
            return Invalid(error ?? "--config, --app-version and --expected-schema are required.");

        var absoluteConfigPath = Path.GetFullPath(configPath);
        var bootstrapSecretFile = options.GetValueOrDefault("--bootstrap-secret-file");
        if (!string.IsNullOrWhiteSpace(bootstrapSecretFile))
            bootstrapSecretFile = Path.GetFullPath(bootstrapSecretFile);

        try
        {
            var result = arguments[0].ToLowerInvariant() switch
            {
                "upgrade-database" or "prepare-database" =>
                    await _probe.PrepareDatabaseAsync(
                        absoluteConfigPath, appVersion, expectedSchema, bootstrapSecretFile, cancellationToken),
                "finalize-database" =>
                    await _probe.FinalizeDatabaseAsync(
                        absoluteConfigPath, appVersion, expectedSchema, cancellationToken),
                "rollback-database" =>
                    await _probe.RollbackDatabaseAsync(
                        absoluteConfigPath, appVersion, expectedSchema, cancellationToken),
                _ => new SetupProbeResult(SetupExitCode.InvalidArguments, "Unknown database cutover command.")
            };
            return FromProbe(result);
        }
        catch (Exception ex)
        {
            return Result(SetupExitCode.MigrationFailed, "Database cutover failed.", detail: ex.Message);
        }
    }
    private SetupExecutionResult WriteConfig(IReadOnlyList<string> arguments)
    {
        if (!TryParseOptions(
                arguments,
                ["--server", "--database", "--auth", "--config", "--log"],
                out var options,
                out var error)
            || !Required(options, "--server", out var server)
            || !Required(options, "--database", out var database)
            || !Required(options, "--auth", out var authenticationText))
        {
            return Invalid(error ?? "--server, --database and --auth are required.");
        }

        if (!Enum.TryParse<DatabaseAuthentication>(
                authenticationText,
                ignoreCase: true,
                out var authentication)
            || authentication is not (DatabaseAuthentication.Windows or DatabaseAuthentication.SqlPassword))
        {
            return Invalid("--auth must be Windows or SqlPassword.");
        }

        try
        {
            var path = Path.GetFullPath(
                options.GetValueOrDefault("--config") ?? _defaultConfigPath());
            // file cấu hình chỉ lưu server, database và kiểu xác thực; mật khẩu SQL nằm trong Credential Manager.
            var settings = WareProSettings.CreateDefault();
            settings.Database.Server = server;
            settings.Database.Database = database;
            settings.Database.Authentication = authentication;
            _writer.Save(path, settings);
            return Result(SetupExitCode.Success, "Configuration was written.", path);
        }
        catch (Exception ex)
        {
            return Result(
                SetupExitCode.ConfigWriteFailed,
                "Configuration could not be written.",
                detail: ex.Message);
        }
    }

    private async Task<SetupExecutionResult> TestConnectionAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (!TryParseOptions(arguments, ["--config", "--mode", "--log"], out var options, out var error)
            || !Required(options, "--config", out var configPath))
        {
            return Invalid(error ?? "--config is required.");
        }

        // mặc định an toàn là app-only để helper không tự suy ra quyền tạo database.
        var modeText = options.GetValueOrDefault("--mode") ?? "app-only";
        var mode = modeText.ToLowerInvariant() switch
        {
            "full" => SetupMode.Full,
            "app-only" => SetupMode.AppOnly,
            _ => (SetupMode?)null
        };
        if (mode is null)
        {
            return Invalid("--mode must be full or app-only.");
        }

        try
        {
            var absolutePath = Path.GetFullPath(configPath);
            var result = await _probe.TestConnectionAsync(
                absolutePath,
                mode.Value,
                cancellationToken);
            return FromProbe(result);
        }
        catch (Exception ex)
        {
            return Result(
                SetupExitCode.ConnectionFailed,
                "Database connection failed.",
                detail: ex.Message);
        }
    }

    private static bool TryParseOptions(
        IReadOnlyList<string> arguments,
        IReadOnlyCollection<string> allowedOptions,
        out Dictionary<string, string> options,
        out string? error)
    {
        // parser đọc theo cặp --name value, từ chối option lạ, thiếu value và option lặp.
        options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;
        for (var index = 1; index < arguments.Count; index += 2)
        {
            var name = arguments[index];
            if (!name.StartsWith("--", StringComparison.Ordinal)
                || !allowedOptions.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                error = "Unknown option.";
                return false;
            }

            if (index + 1 >= arguments.Count
                || arguments[index + 1].StartsWith("--", StringComparison.Ordinal)
                || !options.TryAdd(name, arguments[index + 1]))
            {
                error = "Every option needs one value and can only appear once.";
                return false;
            }
        }

        return true;
    }

    private static bool Required(
        IReadOnlyDictionary<string, string> options,
        string name,
        out string value)
    {
        value = options.GetValueOrDefault(name) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static SetupExecutionResult FromProbe(SetupProbeResult result) =>
        Result(result.ExitCode, result.Message, result.Target, result.TechnicalDetail);

    private static SetupExecutionResult Invalid(string message) =>
        Result(SetupExitCode.InvalidArguments, message);

    private static SetupExecutionResult Result(
        SetupExitCode code,
        string message,
        string? target = null,
        string? detail = null)
    {
        // detail được lọc trước khi trả cho Program ghi log; stdout chỉ chứa summary có trường cố định.
        var redactedDetail = SensitiveDataRedactor.Redact(detail);
        var summary = JsonSerializer.Serialize(
            new
            {
                code = (int)code,
                status = code == SetupExitCode.Success ? "success" : "error",
                message,
                target
            },
            JsonOptions);
        return new SetupExecutionResult(code, summary, redactedDetail);
    }
}

/// <summary>
/// ghi cấu hình bằng store nguyên tử rồi siết ACL của file vừa tạo.
/// </summary>
public sealed class SetupConfigWriter : ISetupConfigWriter
{
    public void Save(string path, WareProSettings settings)
    {
        var absolutePath = Path.GetFullPath(path);
        new WareProSettingsStore(absolutePath).Save(settings);
        ConfigurationAcl.Harden(absolutePath);
    }
}

/// <summary>
/// chỉ SYSTEM và Administrators được sửa; Users chỉ có quyền đọc để ứng dụng khởi động.
/// </summary>
internal static class ConfigurationAcl
{
    public static void Harden(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var security = new FileSecurity();
        // tắt kế thừa trước khi thêm ba rule rõ ràng, tránh quyền ghi rộng từ thư mục cha.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.ReadAndExecute,
            AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }
}

/// <summary>
/// xác nhận instance SQL cục bộ, phiên bản hỗ trợ và quyền cần thiết cho chế độ cài đã chọn.
/// </summary>
public sealed class SqlSetupProbe : ISetupProbe
{
    private const string InstanceRegistryPath =
        @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";

    public async Task<SetupProbeResult> DetectSqlAsync(
        string instance,
        CancellationToken cancellationToken)
    {
        // chỉ nhận instance local và kiểm tra registry trước để không chờ timeout cho tên máy chủ tùy ý.
        if (!IsLocalInstance(instance, out var instanceName)
            || !InstanceExists(instanceName))
        {
            return new SetupProbeResult(
                SetupExitCode.SqlInstanceNotFound,
                "SQL instance was not found.",
                instance);
        }

        try
        {
            // detect dùng Windows authentication tới master vì bước này chạy trước khi app có credential riêng.
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = instance,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 3
            };
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                """
                SELECT
                    TRY_CONVERT(INT, SERVERPROPERTY('ProductMajorVersion')),
                    CONVERT(NVARCHAR(128), SERVERPROPERTY('Edition'));
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)
                || reader.IsDBNull(0)
                || reader.IsDBNull(1)
                || !IsSupportedFullInstall(reader.GetInt32(0), reader.GetString(1)))
            {
                return new SetupProbeResult(
                    SetupExitCode.SqlVersionUnsupported,
                    "Full install requires SQL Server 2022 or newer Express edition.",
                    instance);
            }

            return new SetupProbeResult(
                SetupExitCode.Success,
                "SQL instance is ready.",
                instance);
        }
        catch (Exception ex)
        {
            return new SetupProbeResult(
                SetupExitCode.SqlServiceUnavailable,
                "SQL service is unavailable.",
                instance,
                ex.Message);
        }
    }

    // full install yêu cầu SQL Server 2022 major 16 trở lên và đúng Express edition được hỗ trợ.
    public static bool IsSupportedFullInstall(int productMajorVersion, string edition) =>
        productMajorVersion >= 16
        && edition.Contains("Express Edition", StringComparison.OrdinalIgnoreCase);

    public Task<SetupProbeResult> UpgradeDatabaseAsync(
        string configPath,
        Version appVersion,
        int expectedSchema,
        CancellationToken cancellationToken) =>
        PrepareDatabaseAsync(configPath, appVersion, expectedSchema, null, cancellationToken);

    public async Task<SetupProbeResult> PrepareDatabaseAsync(
        string configPath,
        Version appVersion,
        int expectedSchema,
        string? bootstrapSecretFile,
        CancellationToken cancellationToken) =>
        await RunCutoverAsync(
            configPath,
            connectionString => DatabaseUpgradeRunner.PrepareAsync(
                connectionString, appVersion, expectedSchema, bootstrapSecretFile, cancellationToken));

    public async Task<SetupProbeResult> FinalizeDatabaseAsync(
        string configPath,
        Version appVersion,
        int expectedSchema,
        CancellationToken cancellationToken) =>
        await RunCutoverAsync(
            configPath,
            connectionString => DatabaseUpgradeRunner.FinalizeAsync(
                connectionString, appVersion, expectedSchema, cancellationToken));

    public async Task<SetupProbeResult> RollbackDatabaseAsync(
        string configPath,
        Version appVersion,
        int expectedSchema,
        CancellationToken cancellationToken) =>
        await RunCutoverAsync(
            configPath,
            connectionString => DatabaseUpgradeRunner.RollbackAsync(
                connectionString, appVersion, expectedSchema, cancellationToken));

    private static async Task<SetupProbeResult> RunCutoverAsync(
        string configPath,
        Func<string, Task> operation)
    {
        try
        {
            var settings = new WareProSettingsStore(configPath).Load()
                ?? throw new InvalidOperationException("Configuration is invalid.");
            var connectionString = new ConnectionStringFactory(
                new SqlCredentialStore(),
                () => null,
                () => settings).Resolve(settings);
            await operation(connectionString);
            return new SetupProbeResult(SetupExitCode.Success, "Database cutover command completed.");
        }
        catch (WareProCredentialException ex)
        {
            return new SetupProbeResult(
                SetupExitCode.ValidationFailed,
                "SQL credential must be saved in Windows Credential Manager before installation.",
                TechnicalDetail: ex.ToString());
        }
        catch (DatabaseUpgradeException ex)
        {
            return new SetupProbeResult(ex.ExitCode, ex.Message, TechnicalDetail: ex.ToString());
        }
        catch (Exception ex)
        {
            return new SetupProbeResult(
                SetupExitCode.MigrationFailed,
                "Database cutover command failed.",
                TechnicalDetail: ex.Message);
        }
    }
    public async Task<SetupProbeResult> TestConnectionAsync(
        string configPath,
        SetupMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            // test dùng chính file vừa ghi và cùng ConnectionStringFactory như ứng dụng thật.
            var settings = new WareProSettingsStore(configPath).Load()
                ?? throw new WareProConfigurationException(configPath);
            var factory = new ConnectionStringFactory(
                new SqlCredentialStore(),
                () => null,
                () => settings);
            var targetBuilder = new SqlConnectionStringBuilder(factory.Resolve(settings))
            {
                ConnectTimeout = 5
            };
            var databaseName = targetBuilder.InitialCatalog;
            var masterBuilder = new SqlConnectionStringBuilder(targetBuilder.ConnectionString)
            {
                InitialCatalog = "master"
            };

            // mở master trước để kiểm tra database tồn tại và quyền CREATE ANY DATABASE mà không cần database đích.
            await using var master = new SqlConnection(masterBuilder.ConnectionString);
            await master.OpenAsync(cancellationToken);
            var databaseExists = await DatabaseExistsAsync(master, databaseName, cancellationToken);
            if (!databaseExists)
            {
                // app-only không được tạo database; người dùng phải trỏ tới database đã có.
                if (mode == SetupMode.AppOnly)
                {
                    return new SetupProbeResult(
                        SetupExitCode.ConnectionFailed,
                        "The target database does not exist.",
                        databaseName);
                }

                // full chưa tạo database tại đây, chỉ xác nhận tài khoản có đủ quyền cho bước cài tiếp theo.
                return await CanCreateDatabaseAsync(master, cancellationToken)
                    ? new SetupProbeResult(
                        SetupExitCode.Success,
                        "SQL is ready to create the database.",
                        databaseName)
                    : new SetupProbeResult(
                        SetupExitCode.InsufficientDatabasePermission,
                        "The account cannot create the database.",
                        databaseName);
            }

            // database tồn tại vẫn phải mở được và đọc catalog cơ bản, không chỉ đăng nhập thành công vào master.
            await using var target = new SqlConnection(targetBuilder.ConnectionString);
            await target.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT TOP (1) 1 FROM sys.tables;", target);
            await command.ExecuteScalarAsync(cancellationToken);
            return new SetupProbeResult(
                SetupExitCode.Success,
                "Database connection is ready.",
                databaseName);
        }
        // các mã quyền phổ biến được tách riêng để bộ cài hướng dẫn đúng thay vì báo kết nối chung.
        catch (SqlException ex) when (IsPermissionFailure(ex))
        {
            return new SetupProbeResult(
                SetupExitCode.InsufficientDatabasePermission,
                "The database account has insufficient permission.",
                TechnicalDetail: ex.Message);
        }
        catch (Exception ex)
        {
            return new SetupProbeResult(
                SetupExitCode.ConnectionFailed,
                "Database connection failed.",
                TechnicalDetail: ex.Message);
        }
    }

    private static async Task<bool> DatabaseExistsAsync(
        SqlConnection connection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT CASE WHEN DB_ID(@database) IS NULL THEN 0 ELSE 1 END;",
            connection);
        command.Parameters.Add("@database", SqlDbType.NVarChar, 128).Value = databaseName;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<bool> CanCreateDatabaseAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT HAS_PERMS_BY_NAME(NULL, 'SERVER', 'CREATE ANY DATABASE');",
            connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static bool IsPermissionFailure(SqlException exception) =>
        exception.Errors.Cast<SqlError>().Any(error => error.Number is 229 or 262 or 916);

    // chuẩn hóa default instance thành MSSQLSERVER; named instance giữ phần sau dấu gạch chéo ngược.
    private static bool IsLocalInstance(string value, out string instanceName)
    {
        instanceName = string.Empty;
        var parts = value.Split('\\', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            if (parts[0] is "." or "(local)" or "localhost")
            {
                instanceName = "MSSQLSERVER";
                return true;
            }

            return false;
        }

        if (parts[0] is not ("." or "(local)" or "localhost"))
        {
            return false;
        }

        instanceName = parts[1];
        return !string.IsNullOrWhiteSpace(instanceName);
    }

    // đọc cả registry view 64-bit và 32-bit để helper hoạt động với cách cài SQL khác nhau.
    private static bool InstanceExists(string instanceName) =>
        InstanceExists(instanceName, RegistryView.Registry64)
        || InstanceExists(instanceName, RegistryView.Registry32);

    private static bool InstanceExists(string instanceName, RegistryView view)
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var key = root.OpenSubKey(InstanceRegistryPath, writable: false);
        return key?.GetValue(instanceName) is string value && !string.IsNullOrWhiteSpace(value);
    }
}

internal sealed class DatabaseUpgradeException : Exception
{
    public DatabaseUpgradeException(SetupExitCode exitCode, string message) : base(message) => ExitCode = exitCode;
    public DatabaseUpgradeException(SetupExitCode exitCode, string message, Exception innerException) : base(message, innerException) => ExitCode = exitCode;
    public SetupExitCode ExitCode { get; }
}

internal static class MaintenanceCommandTimeouts
{
    // tách timeout theo loại việc để catalog báo lỗi sớm nhưng backup/restore vẫn đủ thời gian trên database lớn.
    private const int DefaultCatalogSeconds = 60;
    private const int DefaultMigrationSeconds = 300;
    private const int DefaultBackupSeconds = 600;
    private const int DefaultVerifySeconds = 300;
    private const int DefaultRestoreSeconds = 600;

    public static int CatalogSeconds => Read("WAREPRO_SQL_CATALOG_TIMEOUT_SECONDS", DefaultCatalogSeconds);
    public static int MigrationSeconds => Read("WAREPRO_SQL_MIGRATION_TIMEOUT_SECONDS", DefaultMigrationSeconds);
    public static int BackupSeconds => Read("WAREPRO_SQL_BACKUP_TIMEOUT_SECONDS", DefaultBackupSeconds);
    public static int VerifySeconds => Read("WAREPRO_SQL_VERIFY_TIMEOUT_SECONDS", DefaultVerifySeconds);
    public static int RestoreSeconds => Read("WAREPRO_SQL_RESTORE_TIMEOUT_SECONDS", DefaultRestoreSeconds);

    private static int Read(string variable, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(variable), out var value) && value > 0
            ? value
            : fallback;
}
internal static class DatabaseUpgradeRunner
{
    private const int SupportedSchema = 7;

    public static Task RunAsync(
        string connectionString,
        Version appVersion,
        int expectedSchema,
        CancellationToken cancellationToken) =>
        PrepareAsync(connectionString, appVersion, expectedSchema, null, cancellationToken);

    public static async Task PrepareAsync(
        string connectionString,
        Version appVersion,
        int expectedSchema,
        string? bootstrapSecretFile,
        CancellationToken cancellationToken)
    {
        ValidateRelease(appVersion, expectedSchema);
        // đọc secret để tạo hash; finally luôn thử xóa file nguồn, lỗi xóa không che lỗi chính.
        string? bootstrapHash = null;
        if (!string.IsNullOrWhiteSpace(bootstrapSecretFile))
        {
            try
            {
                var password = await File.ReadAllTextAsync(bootstrapSecretFile, cancellationToken);
                if (password.Length < 12)
                    throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "Bootstrap admin password must contain at least 12 characters.");
                bootstrapHash = BCrypt.Net.BCrypt.HashPassword(password);
                password = string.Empty;
            }
            finally
            {
                try { File.Delete(bootstrapSecretFile); } catch { }
            }
        }

        // Prepared đúng release thì chạy lại thành công; mọi Preparing còn dở và Prepared của release khác phải rollback.
        var existingCutover = await ResolveExistingCutoverAsync(connectionString, cancellationToken);
        if (existingCutover is { Status: "Prepared" }
            && existingCutover.PreparedByVersion == appVersion.ToString(3)
            && existingCutover.ExpectedSchema == expectedSchema)
            return;
        if (existingCutover is { Status: "Preparing" or "Prepared" })
            await RollbackAsync(connectionString, appVersion, expectedSchema, cancellationToken);

        string? backupPath = null;
        var installerCreatedDatabase = false;
        var accessRestricted = false;
        // backupPath và hai cờ chọn nhánh recovery theo thứ tự: restore backup, xóa database mới tạo, rồi mở lại quyền truy cập.
        try
        {
            var opened = await OpenConnectionWithCreationAsync(connectionString, cancellationToken);
            installerCreatedDatabase = opened.CreatedDatabase;
            await using var inspection = opened.Connection;
            await SetMaintenanceContextAsync(inspection, cancellationToken);
            await AcquireMaintenanceLockAsync(inspection, cancellationToken);
            var classification = await ClassifyDatabaseAsync(inspection, cancellationToken);
            if (classification == DatabaseClassification.Unrelated)
                throw new DatabaseUpgradeException(
                    SetupExitCode.ValidationFailed,
                    "The target database is not empty and does not have a valid WarePro signature.");

            await DeleteStaleSessionsAsync(inspection, cancellationToken);
            if (await CountActiveSessionsAsync(inspection, cancellationToken) > 0)
                throw new DatabaseUpgradeException(SetupExitCode.ActiveClients, "Active clients are still connected.");

            // restricted mode kicks unregistered legacy connections before backup or DDL and stays until finalize/rollback.
            await inspection.DisposeAsync();
            await SetDatabaseAccessAsync(connectionString, restricted: true, cancellationToken);
            accessRestricted = true;

            await using var connection = await OpenConnectionAsync(connectionString, cancellationToken);
            await SetMaintenanceContextAsync(connection, cancellationToken);
            await AcquireMaintenanceLockAsync(connection, cancellationToken);
            if (!installerCreatedDatabase)
                backupPath = await CreateAndVerifyBackupAsync(connection, cancellationToken);

            // ghi Preparing trước khi đổi RCSI và chạy DDL migration nghiệp vụ.
            await SaveCutoverStateAsync(
                connection, backupPath, appVersion, expectedSchema, "Preparing",
                installerCreatedDatabase, cancellationToken);
            await ExecuteAsync(connection, "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;", cancellationToken);

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                if (classification == DatabaseClassification.Empty)
                {
                    foreach (var batch in DatabaseSchemaScripts.BaselineBatches)
                        await ExecuteAsync(connection, batch, cancellationToken, transaction);
                }

                await ExecuteAsync(
                    connection,
                    DatabaseSchemaScripts.BuildUpgradeSql(expectedSchema, appVersion.ToString(3)),
                    cancellationToken,
                    transaction);

                var hasUsers = Convert.ToInt32(await ExecuteScalarAsync(
                    connection,
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.AppUser) THEN 1 ELSE 0 END;",
                    cancellationToken,
                    transaction)) == 1;
                if (!hasUsers)
                {
                    if (bootstrapHash is null)
                        throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "A bootstrap admin secret is required for a fresh database.");
                    await ExecuteAsync(
                        connection,
                        """
                        INSERT INTO dbo.AppUser
                            (Username, PasswordHash, FullName, RoleCode, IsActive, MustChangePassword)
                        VALUES
                            (N'admin', @passwordHash, N'Quản trị viên', N'Quản trị viên', 1, 1);
                        """,
                        cancellationToken,
                        transaction,
                        ("@passwordHash", bootstrapHash));
                }
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            await ValidateAsync(connection, expectedSchema, cancellationToken);
            await SaveCutoverStateAsync(
                connection, backupPath, appVersion, expectedSchema, "Prepared",
                installerCreatedDatabase, cancellationToken);
        }
        catch (DatabaseUpgradeException)
        {
            await RecoverFailedPrepareAsync(connectionString, backupPath, installerCreatedDatabase, accessRestricted);
            throw;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            await RecoverFailedPrepareAsync(connectionString, backupPath, installerCreatedDatabase, accessRestricted);
            var recovery = backupPath is null ? string.Empty : $" Verified backup retained at {backupPath}.";
            throw new DatabaseUpgradeException(SetupExitCode.MigrationFailed, "Database migration failed." + recovery, ex);
        }
    }
    public static async Task FinalizeAsync(
        string connectionString,
        Version appVersion,
        int expectedSchema,
        CancellationToken cancellationToken)
    {
        ValidateRelease(appVersion, expectedSchema);
        await using (var connection = await OpenConnectionAsync(connectionString, cancellationToken))
        {
            await SetMaintenanceContextAsync(connection, cancellationToken);
            await AcquireMaintenanceLockAsync(connection, cancellationToken);
            await DeleteStaleSessionsAsync(connection, cancellationToken);
            if (await CountActiveSessionsAsync(connection, cancellationToken) > 0)
                throw new DatabaseUpgradeException(SetupExitCode.ActiveClients, "Active clients are still connected.");
            await ValidateAsync(connection, expectedSchema, cancellationToken);

            // finalize chỉ nhận đúng journal Prepared của release này; schema đúng nhưng journal khác vẫn bị từ chối.
            var ready = Convert.ToInt32(await ExecuteScalarAsync(
                connection,
                """
                SELECT CASE WHEN EXISTS
                (
                    SELECT 1 FROM dbo.__WareProUpgradeCutover
                    WHERE Id = 1 AND Status = N'Prepared'
                      AND PreparedByVersion = @version AND ExpectedSchema = @schema
                ) THEN 1 ELSE 0 END;
                """,
                cancellationToken,
                ("@version", appVersion.ToString(3)), ("@schema", expectedSchema)));
            if (ready != 1)
                throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "Database cutover is not prepared for this release.");

            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await ExecuteAsync(
                    connection,
                    DatabaseSchemaScripts.BuildFinalizeSql(expectedSchema, appVersion.ToString(3)),
                    cancellationToken,
                    transaction);
                await ExecuteAsync(
                    connection,
                    "UPDATE dbo.__WareProUpgradeCutover SET Status = N'Finalized', CompletedAtUtc = SYSUTCDATETIME() WHERE Id = 1;",
                    cancellationToken,
                    transaction);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        await SetDatabaseAccessAsync(connectionString, restricted: false, cancellationToken);
    }

    public static async Task RollbackAsync(
        string connectionString,
        Version appVersion,
        int expectedSchema,
        CancellationToken cancellationToken)
    {
        ValidateRelease(appVersion, expectedSchema);
        string? backupPath = null;
        var installerCreatedDatabase = false;
        try
        {
            await using var target = await OpenExistingConnectionAsync(connectionString, cancellationToken);
            if (target is null)
                return;
            await SetMaintenanceContextAsync(target, cancellationToken);
            await AcquireMaintenanceLockAsync(target, cancellationToken);
            await DeleteStaleSessionsAsync(target, cancellationToken);
            if (await CountActiveSessionsAsync(target, cancellationToken) > 0)
                throw new DatabaseUpgradeException(SetupExitCode.ActiveClients, "Active clients are still connected.");
            if (Convert.ToInt32(await ExecuteScalarAsync(
                    target,
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.__WareProUpgradeCutover', N'U') IS NULL THEN 0 ELSE 1 END;",
                    cancellationToken)) == 1)
            {
                backupPath = Convert.ToString(await ExecuteScalarAsync(
                    target,
                    "SELECT TOP (1) BackupPath FROM dbo.__WareProUpgradeCutover WHERE Id = 1;",
                    cancellationToken));
                if (Convert.ToInt32(await ExecuteScalarAsync(target,
                        "SELECT CASE WHEN COL_LENGTH(N'dbo.__WareProUpgradeCutover', N'InstallerCreatedDatabase') IS NULL THEN 0 ELSE 1 END;",
                        cancellationToken)) == 1)
                    installerCreatedDatabase = Convert.ToBoolean(await ExecuteScalarAsync(
                        target,
                        "SELECT TOP (1) InstallerCreatedDatabase FROM dbo.__WareProUpgradeCutover WHERE Id = 1;",
                        cancellationToken));
            }
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            // A previous rollback may already have removed an installer-created database.
            return;
        }

        if (!string.IsNullOrWhiteSpace(backupPath))
            await RestoreBackupAsync(connectionString, backupPath, cancellationToken);
        else if (installerCreatedDatabase)
            await DropDatabaseAsync(connectionString, cancellationToken);
        else
            await SetDatabaseAccessAsync(connectionString, restricted: false, cancellationToken);
    }
    private enum DatabaseClassification
    {
        Empty = 0,
        WarePro = 1,
        LegacyWarePro = 2,
        Unrelated = 3
    }

    private static async Task<DatabaseClassification> ClassifyDatabaseAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        // identity chính thức hoặc journal của database installer vừa tạo được nhận ngay; database cũ phải khớp dấu hiệu riêng.
        // mọi database chỉ giống một phần được xếp Unrelated để tránh chạy DDL nhầm dữ liệu của hệ thống khác.
        var value = Convert.ToInt32(await ExecuteScalarAsync(connection, """
            DECLARE @UserTableCount int =
            (
                SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0
            );
            IF @UserTableCount = 0
            BEGIN
                SELECT 0;
                RETURN;
            END;

            DECLARE @Owned bit = 0;
            IF OBJECT_ID(N'dbo.__WareProDatabaseIdentity', N'U') IS NOT NULL
                EXEC sys.sp_executesql
                    N'SELECT @Owned = CASE WHEN EXISTS
                      (
                          SELECT 1 FROM dbo.__WareProDatabaseIdentity
                          WHERE Id = 1
                            AND ProductId = ''F65EAB95-A3F8-4D8D-9AF5-4839FCA38E21''
                            AND ProductName = N''WarePro''
                      ) THEN 1 ELSE 0 END;',
                    N'@Owned bit OUTPUT',
                    @Owned = @Owned OUTPUT;
            IF @Owned = 1
            BEGIN
                SELECT 1;
                RETURN;
            END;

            DECLARE @InstallerCreated bit = 0;
            IF OBJECT_ID(N'dbo.__WareProUpgradeCutover', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.__WareProUpgradeCutover', N'InstallerCreatedDatabase') IS NOT NULL
                EXEC sys.sp_executesql
                    N'SELECT @InstallerCreated = CASE WHEN EXISTS
                      (
                          SELECT 1 FROM dbo.__WareProUpgradeCutover
                          WHERE Id = 1 AND InstallerCreatedDatabase = 1
                            AND Status IN (N''Preparing'', N''Prepared'')
                      ) THEN 1 ELSE 0 END;',
                    N'@InstallerCreated bit OUTPUT',
                    @InstallerCreated = @InstallerCreated OUTPUT;
            IF @InstallerCreated = 1
            BEGIN
                SELECT 1;
                RETURN;
            END;

            DECLARE @DistinctiveLegacyShape bit = 0;
            IF OBJECT_ID(N'dbo.ProductUnit', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.ProductUnit', N'ConversionFactor') IS NOT NULL
               AND EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ProductUnit_ConversionFactor_Positive')
               AND OBJECT_ID(N'dbo.StockIn', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StockIn', N'DocumentCode') IS NOT NULL
               AND OBJECT_ID(N'dbo.StockInLine', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StockInLine', N'BaseQuantity') IS NOT NULL
               AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StockInLine_StockIn' AND parent_object_id = OBJECT_ID(N'dbo.StockInLine'))
               AND OBJECT_ID(N'dbo.StockOut', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StockOut', N'DocumentCode') IS NOT NULL
               AND OBJECT_ID(N'dbo.StockOutLine', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StockOutLine', N'BaseQuantity') IS NOT NULL
               AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_StockOutLine_StockOut' AND parent_object_id = OBJECT_ID(N'dbo.StockOutLine'))
               AND OBJECT_ID(N'dbo.StockLedger', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StockLedger', N'SourceDocumentType') IS NOT NULL
               AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockLedger_Warehouse_Product_PostedAt' AND object_id = OBJECT_ID(N'dbo.StockLedger'))
               AND EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockLedger_SourceDocument' AND object_id = OBJECT_ID(N'dbo.StockLedger'))
                SET @DistinctiveLegacyShape = 1;

            SELECT CASE WHEN @DistinctiveLegacyShape = 1 THEN 2 ELSE 3 END;
            """, cancellationToken));
        return (DatabaseClassification)value;
    }    private static void ValidateRelease(Version appVersion, int expectedSchema)
    {
        if (expectedSchema != SupportedSchema || appVersion < new Version("1.1.0"))
            throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "Release compatibility validation failed.");
    }

    private static Task SetMaintenanceContextAsync(SqlConnection connection, CancellationToken token) =>
        ExecuteAsync(
            connection,
            "EXEC sys.sp_set_session_context @key = N'WareProMaintenance', @value = 1;",
            token);

    private static async Task AcquireMaintenanceLockAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        // applock theo database chặn hai bộ cài cùng bảo trì một catalog.
        // LockTimeout = 0 trả lỗi ngay để Inno Setup không treo chờ một phiên cài khác.
        await using var command = new SqlCommand("""
            DECLARE @result INT;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = 0;
            SELECT @result;
            """, connection);
        command.CommandTimeout = MaintenanceCommandTimeouts.CatalogSeconds;
        command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value =
            "WAREPRO:SCHEMAMAINTENANCE:" + connection.Database.Trim().ToUpperInvariant();
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) < 0)
            throw new DatabaseUpgradeException(SetupExitCode.ActiveClients, "Active clients are still connected.");
    }

    private static Task DeleteStaleSessionsAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.__WareProClientSession', N'U') IS NOT NULL
                EXEC sys.sp_executesql
                    N'DELETE FROM dbo.__WareProClientSession WHERE LastSeenUtc < DATEADD(SECOND, -90, SYSUTCDATETIME());';
            """, cancellationToken);

    private static async Task<int> CountActiveSessionsAsync(SqlConnection connection, CancellationToken cancellationToken) =>
        Convert.ToInt32(await ExecuteScalarAsync(connection, """
            DECLARE @ActiveSessions INT = 0;
            IF OBJECT_ID(N'dbo.__WareProClientSession', N'U') IS NOT NULL
                EXEC sys.sp_executesql
                    N'SELECT @value = COUNT(*) FROM dbo.__WareProClientSession;',
                    N'@value INT OUTPUT',
                    @value = @ActiveSessions OUTPUT;
            SELECT @ActiveSessions;
            """, cancellationToken));

    private static async Task<string> CreateAndVerifyBackupAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        string? path = null;
        try
        {
            var directory = Convert.ToString(await ExecuteScalarAsync(
                connection,
                "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));",
                cancellationToken));
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("SQL Server backup directory is unavailable.");

            // timestamp giúp tra cứu; guid ngăn hai lần prepare cùng mili giây ghi đè cùng file.
            path = Path.Combine(directory, $"WarePro-{connection.Database}-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.bak");
            var database = connection.Database.Replace("]", "]]", StringComparison.Ordinal);
            await ExecuteAsync(connection, $"BACKUP DATABASE [{database}] TO DISK = @path WITH CHECKSUM, INIT;", cancellationToken, MaintenanceCommandTimeouts.BackupSeconds, ("@path", path));
            await ExecuteAsync(connection, "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;", cancellationToken, MaintenanceCommandTimeouts.VerifySeconds, ("@path", path));
            return path;
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            throw new DatabaseUpgradeException(SetupExitCode.BackupFailed, "Database backup or verification failed.", ex);
        }
    }

    // journal là nguồn phục hồi qua lần chạy mới; cùng release ở trạng thái Preparing/Prepared mới giữ lại backup và cờ database đã tạo.
    private static Task SaveCutoverStateAsync(
        SqlConnection connection,
        string? backupPath,
        Version appVersion,
        int expectedSchema,
        string status,
        bool installerCreatedDatabase,
        CancellationToken token) =>
        ExecuteAsync(connection, """
            IF OBJECT_ID(N'dbo.__WareProUpgradeCutover', N'U') IS NULL
                CREATE TABLE dbo.__WareProUpgradeCutover
                (
                    Id int NOT NULL CONSTRAINT PK___WareProUpgradeCutover PRIMARY KEY,
                    Status nvarchar(32) NOT NULL,
                    BackupPath nvarchar(4000) NULL,
                    PreparedByVersion nvarchar(32) NOT NULL,
                    ExpectedSchema int NOT NULL,
                    InstallerCreatedDatabase bit NOT NULL CONSTRAINT DF___WareProUpgradeCutover_InstallerCreated DEFAULT (0),
                    PreparedAtUtc datetime2(0) NOT NULL,
                    CompletedAtUtc datetime2(0) NULL
                );
            IF COL_LENGTH(N'dbo.__WareProUpgradeCutover', N'ExpectedSchema') IS NULL
                ALTER TABLE dbo.__WareProUpgradeCutover ADD ExpectedSchema int NULL;
            IF COL_LENGTH(N'dbo.__WareProUpgradeCutover', N'InstallerCreatedDatabase') IS NULL
                ALTER TABLE dbo.__WareProUpgradeCutover ADD InstallerCreatedDatabase bit NOT NULL
                    CONSTRAINT DF___WareProUpgradeCutover_InstallerCreated DEFAULT (0);
            MERGE dbo.__WareProUpgradeCutover AS target
            USING (SELECT 1 AS Id) AS source ON target.Id = source.Id
            WHEN MATCHED THEN UPDATE SET
                Status = @status,
                BackupPath = CASE
                    WHEN target.Status IN (N'Preparing', N'Prepared')
                     AND target.PreparedByVersion = @version
                     AND target.ExpectedSchema = @schema
                    THEN COALESCE(target.BackupPath, @backupPath) ELSE @backupPath END,
                PreparedByVersion = @version, ExpectedSchema = @schema,
                InstallerCreatedDatabase = CASE
                    WHEN target.Status IN (N'Preparing', N'Prepared')
                     AND target.PreparedByVersion = @version
                     AND target.ExpectedSchema = @schema
                    THEN target.InstallerCreatedDatabase | @installerCreated ELSE @installerCreated END,
                PreparedAtUtc = SYSUTCDATETIME(), CompletedAtUtc = NULL
            WHEN NOT MATCHED THEN INSERT
                (Id, Status, BackupPath, PreparedByVersion, ExpectedSchema, InstallerCreatedDatabase, PreparedAtUtc)
                VALUES (1, @status, @backupPath, @version, @schema, @installerCreated, SYSUTCDATETIME());
            """, token,
            ("@status", status), ("@backupPath", (object?)backupPath ?? DBNull.Value),
            ("@version", appVersion.ToString(3)), ("@schema", expectedSchema),
            ("@installerCreated", installerCreatedDatabase));    private static async Task ValidateAsync(SqlConnection connection, int expectedSchema, CancellationToken cancellationToken)
    {
        try
        {
            var shapePredicate = DatabaseSchemaScripts.ShapeValidationPredicate;
            var validationContract = shapePredicate + DatabaseSchemaScripts.BuildUpgradeSql(expectedSchema, "1.1.0");
            foreach (var marker in new[]
                     {
                         "TYPE_NAME", "max_length", "AuditArchiveManifest", "UX_AuditArchiveManifest_OperationId",
                         "FK_StockTransfer_FromWarehouse", "FK_StockTransferLine_StockTransfer", "RowVersion"
                     })
                if (!validationContract.Contains(marker, StringComparison.Ordinal))
                    throw new InvalidOperationException("Schema validation predicate is incomplete.");

            var valid = Convert.ToInt32(await ExecuteScalarAsync(connection, $"""
                SELECT CASE WHEN
                    (SELECT Version FROM dbo.__WareProSchemaVersion WHERE Id = 1) = @schema
                    AND OBJECT_ID(N'dbo.AppUser', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.Product', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.Warehouse', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.StockBalance', N'U') IS NOT NULL
                    AND OBJECT_ID(N'dbo.__WareProClientSession', N'U') IS NOT NULL
                    AND COL_LENGTH(N'dbo.AppUser', N'RowVersion') IS NOT NULL
                    AND COL_LENGTH(N'dbo.Product', N'RowVersion') IS NOT NULL
                    AND COL_LENGTH(N'dbo.StockBalance', N'RowVersion') IS NOT NULL
                    AND ({shapePredicate})
                    THEN 1 ELSE 0 END;
                """, cancellationToken, ("@schema", expectedSchema)));
            if (valid != 1)
                throw new InvalidOperationException("Schema shape or release metadata does not match.");
        }
        catch (Exception ex)
        {
            throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "Database validation failed.", ex);
        }
    }

    private static async Task SetDatabaseAccessAsync(
        string connectionString,
        bool restricted,
        CancellationToken token)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var database = target.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
            throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "Target database is required.");
        var escaped = database.Replace("]", "]]", StringComparison.Ordinal);
        target.InitialCatalog = "master";
        await using var master = new SqlConnection(target.ConnectionString);
        await master.OpenAsync(token);
        await ExecuteAsync(
            master,
            $"ALTER DATABASE [{escaped}] SET {(restricted ? "RESTRICTED_USER" : "MULTI_USER")} WITH ROLLBACK IMMEDIATE;",
            token);
    }

    private static async Task RestoreBackupAsync(
        string connectionString,
        string backupPath,
        CancellationToken token)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var database = target.InitialCatalog;
        var escaped = database.Replace("]", "]]", StringComparison.Ordinal);
        target.InitialCatalog = "master";
        await using var master = new SqlConnection(target.ConnectionString);
        await master.OpenAsync(token);
        await ExecuteAsync(master, $"ALTER DATABASE [{escaped}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", token, MaintenanceCommandTimeouts.RestoreSeconds);
        await ExecuteAsync(master, $"RESTORE DATABASE [{escaped}] FROM DISK = @path WITH REPLACE, CHECKSUM;", token, MaintenanceCommandTimeouts.RestoreSeconds, ("@path", backupPath));
        await ExecuteAsync(master, $"ALTER DATABASE [{escaped}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;", token, MaintenanceCommandTimeouts.RestoreSeconds);
    }

    private static async Task DropDatabaseAsync(string connectionString, CancellationToken token)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        var database = target.InitialCatalog;
        var escaped = database.Replace("]", "]]", StringComparison.Ordinal);
        target.InitialCatalog = "master";
        await using var master = new SqlConnection(target.ConnectionString);
        await master.OpenAsync(token);
        await ExecuteAsync(master, $"ALTER DATABASE [{escaped}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{escaped}];", token);
    }

    private static async Task RecoverFailedPrepareAsync(
        string connectionString,
        string? backupPath,
        bool installerCreatedDatabase,
        bool accessRestricted)
    {
        // recovery bỏ cancellation của lệnh gốc để vẫn khôi phục sau khi người dùng hủy cài đặt.
        // nếu recovery lỗi, giữ lỗi ban đầu; database có thể còn restricted hoặc single-user và cần phục hồi thủ công.
        try
        {
            if (!string.IsNullOrWhiteSpace(backupPath))
                await RestoreBackupAsync(connectionString, backupPath, CancellationToken.None);
            else if (installerCreatedDatabase)
                await DropDatabaseAsync(connectionString, CancellationToken.None);
            else if (accessRestricted)
                await SetDatabaseAccessAsync(connectionString, restricted: false, CancellationToken.None);
        }
        catch
        {
            // Keep original failure. A retained backup or restricted database remains fail-closed for manual recovery.
        }
    }

    private static async Task<SqlConnection> OpenConnectionAsync(string connectionString, CancellationToken cancellationToken) =>
        (await OpenConnectionWithCreationAsync(connectionString, cancellationToken)).Connection;

    private static async Task<(SqlConnection Connection, bool CreatedDatabase)> OpenConnectionWithCreationAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var target = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(target.InitialCatalog))
            throw new DatabaseUpgradeException(SetupExitCode.ValidationFailed, "Target database is required.");

        var existing = await OpenExistingConnectionAsync(connectionString, cancellationToken);
        if (existing is not null)
            return (existing, false);

        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        await using var masterConnection = new SqlConnection(master.ConnectionString);
        await masterConnection.OpenAsync(cancellationToken);
        var database = target.InitialCatalog.Replace("]", "]]", StringComparison.Ordinal);
        await using var command = new SqlCommand($"CREATE DATABASE [{database}];", masterConnection)
        {
            CommandTimeout = MaintenanceCommandTimeouts.MigrationSeconds
        };
        await command.ExecuteNonQueryAsync(cancellationToken);

        var created = new SqlConnection(target.ConnectionString);
        await created.OpenAsync(cancellationToken);
        return (created, true);
    }

    private static async Task<SqlConnection?> OpenExistingConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch (SqlException ex) when (ex.Number == 4060)
        {
            await connection.DisposeAsync();
            return null;
        }
    }

    private sealed record CutoverState(
        string Status,
        string? BackupPath,
        string PreparedByVersion,
        int ExpectedSchema,
        bool InstallerCreatedDatabase);

    private static async Task<CutoverState?> ResolveExistingCutoverAsync(
        string connectionString,
        CancellationToken token)
    {
        await using var connection = await OpenExistingConnectionAsync(connectionString, token);
        if (connection is null || Convert.ToInt32(await ExecuteScalarAsync(connection,
                "SELECT CASE WHEN OBJECT_ID(N'dbo.__WareProUpgradeCutover', N'U') IS NULL THEN 0 ELSE 1 END;", token)) == 0)
            return null;

        var hasCreatedFlag = Convert.ToInt32(await ExecuteScalarAsync(connection,
            "SELECT CASE WHEN COL_LENGTH(N'dbo.__WareProUpgradeCutover', N'InstallerCreatedDatabase') IS NULL THEN 0 ELSE 1 END;", token)) == 1;
        var sql = hasCreatedFlag
            ? "SELECT TOP (1) Status, BackupPath, PreparedByVersion, ExpectedSchema, InstallerCreatedDatabase FROM dbo.__WareProUpgradeCutover WHERE Id = 1;"
            : "SELECT TOP (1) Status, BackupPath, PreparedByVersion, ExpectedSchema, CAST(0 AS bit) FROM dbo.__WareProUpgradeCutover WHERE Id = 1;";
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = MaintenanceCommandTimeouts.CatalogSeconds
        };
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token))
            return null;
        return new CutoverState(
            reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetString(2), reader.GetInt32(3), reader.GetBoolean(4));
    }
    private static Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken token,
        params (string Name, object Value)[] parameters) =>
        ExecuteAsync(connection, sql, token, MaintenanceCommandTimeouts.MigrationSeconds, parameters);

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken token,
        int commandTimeout,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = commandTimeout
        };
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        string sql,
        CancellationToken token,
        SqlTransaction transaction,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = MaintenanceCommandTimeouts.MigrationSeconds
        };
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqlConnection connection,
        string sql,
        CancellationToken token,
        params (string Name, object Value)[] parameters)
    {
        await using var command = new SqlCommand(sql, connection)
        {
            CommandTimeout = MaintenanceCommandTimeouts.CatalogSeconds
        };
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync(token);
    }

    private static async Task<object?> ExecuteScalarAsync(
        SqlConnection connection,
        string sql,
        CancellationToken token,
        SqlTransaction transaction)
    {
        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = MaintenanceCommandTimeouts.CatalogSeconds
        };
        return await command.ExecuteScalarAsync(token);
    }
}