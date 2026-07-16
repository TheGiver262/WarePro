using System.Data;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using QuanLyHangHoa.Configuration;

namespace WarePro.SetupHelper;

public enum SetupExitCode
{
    Success = 0,
    InvalidArguments = 10,
    SqlInstanceNotFound = 20,
    SqlServiceUnavailable = 21,
    ConnectionFailed = 22,
    InsufficientDatabasePermission = 23,
    SqlVersionUnsupported = 24,
    ConfigWriteFailed = 30
}

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

public interface ISetupProbe
{
    Task<SetupProbeResult> DetectSqlAsync(
        string instance,
        CancellationToken cancellationToken);

    Task<SetupProbeResult> TestConnectionAsync(
        string configPath,
        SetupMode mode,
        CancellationToken cancellationToken);
}

public interface ISetupConfigWriter
{
    void Save(string path, WareProSettings settings);
}

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

        return arguments[0].ToLowerInvariant() switch
        {
            "detect-sql" => await DetectSqlAsync(arguments, cancellationToken),
            "write-config" => WriteConfig(arguments),
            "test-connection" => await TestConnectionAsync(arguments, cancellationToken),
            _ => Invalid("Unknown command.")
        };
    }

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

public sealed class SetupConfigWriter : ISetupConfigWriter
{
    public void Save(string path, WareProSettings settings)
    {
        var absolutePath = Path.GetFullPath(path);
        new WareProSettingsStore(absolutePath).Save(settings);
        ConfigurationAcl.Harden(absolutePath);
    }
}

internal static class ConfigurationAcl
{
    public static void Harden(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var security = new FileSecurity();
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

public sealed class SqlSetupProbe : ISetupProbe
{
    private const string InstanceRegistryPath =
        @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL";

    public async Task<SetupProbeResult> DetectSqlAsync(
        string instance,
        CancellationToken cancellationToken)
    {
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

    public static bool IsSupportedFullInstall(int productMajorVersion, string edition) =>
        productMajorVersion >= 16
        && edition.Contains("Express Edition", StringComparison.OrdinalIgnoreCase);

    public async Task<SetupProbeResult> TestConnectionAsync(
        string configPath,
        SetupMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
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

            await using var master = new SqlConnection(masterBuilder.ConnectionString);
            await master.OpenAsync(cancellationToken);
            var databaseExists = await DatabaseExistsAsync(master, databaseName, cancellationToken);
            if (!databaseExists)
            {
                if (mode == SetupMode.AppOnly)
                {
                    return new SetupProbeResult(
                        SetupExitCode.ConnectionFailed,
                        "The target database does not exist.",
                        databaseName);
                }

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

            await using var target = new SqlConnection(targetBuilder.ConnectionString);
            await target.OpenAsync(cancellationToken);
            await using var command = new SqlCommand("SELECT TOP (1) 1 FROM sys.tables;", target);
            await command.ExecuteScalarAsync(cancellationToken);
            return new SetupProbeResult(
                SetupExitCode.Success,
                "Database connection is ready.",
                databaseName);
        }
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
