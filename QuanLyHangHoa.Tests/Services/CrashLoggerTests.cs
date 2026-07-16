using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Services;
using System.IO;

namespace QuanLyHangHoa.Tests.Services;

public class CrashLoggerTests : IDisposable
{
    private readonly string _logDirectory = Path.Combine(
        Path.GetTempPath(),
        "WarePro.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Default_options_use_the_user_local_log_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "warepro-crash-paths");
        var paths = WareProPaths.FromRoots(
            Path.Combine(root, "ProgramFiles"),
            Path.Combine(root, "ProgramData"),
            Path.Combine(root, "LocalAppData"));
        var options = CrashLogOptions.CreateDefault(() => paths);

        Assert.Equal(paths.UserLogDirectory, options.PrimaryDirectoryProvider());
        Assert.False(options.PrimaryDirectoryProvider().StartsWith(paths.InstallDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Write_creates_log_with_source_exception_and_diagnostics()
    {
        var exception = new InvalidOperationException(
            "outer failure",
            new ArgumentException("inner failure"));
        var options = CreateOptions(_logDirectory);

        CrashLogger.WriteWithOptions(exception, "test source", options);

        var file = Assert.Single(Directory.GetFiles(_logDirectory, "crash-*.log"));
        var content = File.ReadAllText(file);
        Assert.Contains("test source", content);
        Assert.Contains("outer failure", content);
        Assert.Contains("inner failure", content);
        Assert.Contains("timestampUtc=2026-07-16T03:04:05.0000000+00:00", content);
        Assert.Contains("correlationId=correlation-123", content);
        Assert.Contains("appVersion=1.2.3-test", content);
    }

    [Fact]
    public void Write_redacts_connection_credentials_and_release_tokens()
    {
        var exception = new InvalidOperationException(
            "Server=db;User ID=admin;Password=p@ss;release token=abc123;credential=cred-value");
        var options = CreateOptions(_logDirectory);

        CrashLogger.WriteWithOptions(exception, "credential failure", options);

        var content = File.ReadAllText(Assert.Single(Directory.GetFiles(_logDirectory, "crash-*.log")));
        Assert.DoesNotContain("admin", content, StringComparison.Ordinal);
        Assert.DoesNotContain("p@ss", content, StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", content, StringComparison.Ordinal);
        Assert.DoesNotContain("cred-value", content, StringComparison.Ordinal);
        Assert.Contains("User ID=***", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Password=***", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("token=***", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Write_uses_temp_fallback_when_the_primary_directory_is_invalid()
    {
        var occupiedPath = Path.Combine(_logDirectory, "not-a-directory");
        var fallbackDirectory = Path.Combine(_logDirectory, "fallback");
        Directory.CreateDirectory(_logDirectory);
        File.WriteAllText(occupiedPath, "occupied");
        var options = CreateOptions(occupiedPath, fallbackDirectory);

        var error = Record.Exception((Action)(() =>
            CrashLogger.WriteWithOptions(new Exception("failure"), "test source", options)));

        Assert.Null(error);
        Assert.Single(Directory.GetFiles(fallbackDirectory, "crash-*.log"));
    }

    [Fact]
    public void Write_does_not_throw_when_primary_and_fallback_are_invalid()
    {
        var occupiedPath = Path.Combine(_logDirectory, "not-a-directory");
        Directory.CreateDirectory(_logDirectory);
        File.WriteAllText(occupiedPath, "occupied");
        var options = CreateOptions(occupiedPath, occupiedPath);

        var error = Record.Exception((Action)(() =>
            CrashLogger.WriteWithOptions(new Exception("failure"), "test source", options)));

        Assert.Null(error);
    }

    [Fact]
    public void Product_serial_errors_use_the_shared_crash_logger()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var source = File.ReadAllText(Path.Combine(
            repoRoot, "QuanLyHangHoa", "ViewModels", "ProductSerialViewModel.cs"));

        Assert.DoesNotContain("ProductManagement_Antigravity\\wpf_error.log", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CrashLogger.Write(ex, \"Product serial loading\")", source, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }

    private static CrashLogOptions CreateOptions(string primaryDirectory, string? fallbackDirectory = null) => new()
    {
        PrimaryDirectoryProvider = () => primaryDirectory,
        FallbackDirectoryProvider = () => fallbackDirectory ?? Path.Combine(primaryDirectory, "fallback"),
        UtcNowProvider = () => new DateTimeOffset(2026, 7, 16, 3, 4, 5, TimeSpan.Zero),
        CorrelationIdProvider = () => "correlation-123",
        AppVersionProvider = () => "1.2.3-test"
    };
}
