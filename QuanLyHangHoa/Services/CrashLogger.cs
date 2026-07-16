using System;
using System.IO;
using System.Reflection;
using System.Text;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Services;

public sealed class CrashLogOptions
{
    public required Func<string> PrimaryDirectoryProvider { get; init; }
    public required Func<string> FallbackDirectoryProvider { get; init; }
    public required Func<DateTimeOffset> UtcNowProvider { get; init; }
    public required Func<string> CorrelationIdProvider { get; init; }
    public required Func<string> AppVersionProvider { get; init; }

    public static CrashLogOptions CreateDefault(Func<WareProPaths>? pathsProvider = null)
    {
        pathsProvider ??= () => WareProPaths.Current;
        return new CrashLogOptions
        {
            PrimaryDirectoryProvider = () => pathsProvider().UserLogDirectory,
            FallbackDirectoryProvider = () => Path.Combine(Path.GetTempPath(), "WarePro", "Logs"),
            UtcNowProvider = () => DateTimeOffset.UtcNow,
            CorrelationIdProvider = () => Guid.NewGuid().ToString("N"),
            AppVersionProvider = GetAppVersion
        };
    }

    private static string GetAppVersion()
    {
        var assembly = typeof(CrashLogger).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
    }
}

public static class CrashLogger
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static void Write(Exception exception, string source, string? logDirectory = null)
    {
        var defaults = CrashLogOptions.CreateDefault();
        var options = new CrashLogOptions
        {
            PrimaryDirectoryProvider = logDirectory is null
                ? defaults.PrimaryDirectoryProvider
                : () => logDirectory,
            FallbackDirectoryProvider = defaults.FallbackDirectoryProvider,
            UtcNowProvider = defaults.UtcNowProvider,
            CorrelationIdProvider = defaults.CorrelationIdProvider,
            AppVersionProvider = defaults.AppVersionProvider
        };

        WriteWithOptions(exception, source, options);
    }

    public static void WriteWithOptions(Exception exception, string source, CrashLogOptions options)
    {
        try
        {
            var timestamp = options.UtcNowProvider().ToUniversalTime();
            var entry = new StringBuilder()
                .AppendLine($"timestampUtc={timestamp:O}")
                .AppendLine($"correlationId={SensitiveDataRedactor.Redact(options.CorrelationIdProvider())}")
                .AppendLine($"appVersion={SensitiveDataRedactor.Redact(options.AppVersionProvider())}")
                .AppendLine($"source={SensitiveDataRedactor.Redact(source)}")
                .AppendLine(SensitiveDataRedactor.Redact(exception.ToString()))
                .AppendLine(new string('-', 80))
                .ToString();

            if (TryAppend(options.PrimaryDirectoryProvider(), timestamp, entry))
            {
                return;
            }

            TryAppend(options.FallbackDirectoryProvider(), timestamp, entry);
        }
        catch
        {
        }
    }

    private static bool TryAppend(string directory, DateTimeOffset timestamp, string entry)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"crash-{timestamp:yyyyMMdd}.log");
            File.AppendAllText(path, entry, Utf8WithoutBom);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
