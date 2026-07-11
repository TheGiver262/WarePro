using System;
using System.IO;
using System.Text;

namespace QuanLyHangHoa.Services;

public static class CrashLogger
{
    public static void Write(Exception exception, string source, string? logDirectory = null)
    {
        try
        {
            var directory = logDirectory ?? Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, $"crash-{DateTime.Now:yyyyMMdd}.log");
            var entry = new StringBuilder()
                .AppendLine($"[{DateTime.Now:O}] {source}")
                .AppendLine(exception.ToString())
                .AppendLine(new string('-', 80))
                .ToString();

            File.AppendAllText(path, entry, Encoding.UTF8);
        }
        catch
        {
            // Logging must never replace the original application failure.
        }
    }
}
