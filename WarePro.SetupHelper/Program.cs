using System.Text;

namespace WarePro.SetupHelper;

/// <summary>
/// entry point nhỏ cho bộ cài: chạy một command, in JSON summary và trả exit code ổn định.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // UTF-8 không BOM giữ output đọc được cả trong console lẫn log của Inno Setup.
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var result = await SetupCommands.CreateDefault().ExecuteAsync(args);
        Console.Out.WriteLine(result.Summary);

        var logPath = SetupCommands.FindOption(args, "--log");
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            try
            {
                var command = args.FirstOrDefault() switch
                {
                    "detect-sql" => "detect-sql",
                    "write-config" => "write-config",
                    "test-connection" => "test-connection",
                    "upgrade-database" => "upgrade-database",
                    "prepare-database" => "prepare-database",
                    "finalize-database" => "finalize-database",
                    "rollback-database" => "rollback-database",
                    _ => "unknown"
                };
                var absoluteLogPath = Path.GetFullPath(logPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absoluteLogPath)!);
                var detail = SetupLogRedactor.Redact(result.TechnicalDetail);
                await File.AppendAllTextAsync(
                    absoluteLogPath,
                    $"{DateTimeOffset.UtcNow:O} command={command} exit={(int)result.ExitCode} summary={SetupLogRedactor.Redact(result.Summary)} detail={detail}{Environment.NewLine}",
                    Encoding.UTF8);
            }
            catch
            {
                // Database result remains authoritative when optional audit file cannot be written.
            }
        }
        return (int)result.ExitCode;
    }
}
