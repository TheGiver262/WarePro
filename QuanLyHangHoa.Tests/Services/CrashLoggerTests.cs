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
    public void Write_creates_log_with_source_and_exception_chain()
    {
        var exception = new InvalidOperationException(
            "outer failure",
            new ArgumentException("inner failure"));

        CrashLogger.Write(exception, "test source", _logDirectory);

        var file = Assert.Single(Directory.GetFiles(_logDirectory, "crash-*.log"));
        var content = File.ReadAllText(file);
        Assert.Contains("test source", content);
        Assert.Contains("outer failure", content);
        Assert.Contains("inner failure", content);
    }

    [Fact]
    public void Write_does_not_throw_when_directory_is_invalid()
    {
        var filePath = Path.Combine(_logDirectory, "not-a-directory");
        Directory.CreateDirectory(_logDirectory);
        File.WriteAllText(filePath, "occupied");

        var error = Record.Exception((Action)(() =>
            CrashLogger.Write(new Exception("failure"), "test source", filePath)));

        Assert.Null(error);
    }

    public void Dispose()
    {
        if (Directory.Exists(_logDirectory))
        {
            Directory.Delete(_logDirectory, recursive: true);
        }
    }
}
