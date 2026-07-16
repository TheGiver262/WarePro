using System;
using System.IO;
using System.Reflection;
using System.Text;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Services;

/// <summary>
/// gom các nguồn thời gian, phiên bản và đường dẫn để logger kiểm thử được cả nhánh fallback.
/// </summary>
public sealed class CrashLogOptions
{
    public required Func<string> PrimaryDirectoryProvider { get; init; }
    public required Func<string> FallbackDirectoryProvider { get; init; }
    public required Func<DateTimeOffset> UtcNowProvider { get; init; }
    public required Func<string> CorrelationIdProvider { get; init; }
    public required Func<string> AppVersionProvider { get; init; }

    // log chính nằm theo người dùng; thư mục temp là đường lui khi profile không ghi được.
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

/// <summary>
/// ghi lỗi theo kiểu best-effort và không bao giờ ném thêm ngoại lệ vào luồng đang gặp sự cố.
/// </summary>
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
            // mọi trường có thể đến từ môi trường chạy đều được lọc trước khi ghép vào log.
            var entry = new StringBuilder()
                .AppendLine($"timestampUtc={timestamp:O}")
                .AppendLine($"correlationId={SensitiveDataRedactor.Redact(options.CorrelationIdProvider())}")
                .AppendLine($"appVersion={SensitiveDataRedactor.Redact(options.AppVersionProvider())}")
                .AppendLine($"source={SensitiveDataRedactor.Redact(source)}")
                .AppendLine(SensitiveDataRedactor.Redact(exception.ToString()))
                .AppendLine(new string('-', 80))
                .ToString();

            // chỉ dùng fallback khi đường dẫn chính không tạo hoặc không ghi được.
            if (TryAppend(options.PrimaryDirectoryProvider(), timestamp, entry))
            {
                return;
            }

            // lỗi ở cả hai đường dẫn được bỏ qua vì logger không được che ngoại lệ gốc.
            TryAppend(options.FallbackDirectoryProvider(), timestamp, entry);
        }
        catch
        {
        }
    }

    // mỗi ngày dùng một file và append để giữ các correlation id trong cùng dòng thời gian.
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
