using System.Text;

namespace WarePro.SetupHelper;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var result = await SetupCommands.CreateDefault().ExecuteAsync(args);
        Console.Out.WriteLine(result.Summary);

        var logPath = SetupCommands.FindOption(args, "--log");
        if (!string.IsNullOrWhiteSpace(logPath) && !string.IsNullOrWhiteSpace(result.TechnicalDetail))
        {
            var absoluteLogPath = Path.GetFullPath(logPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteLogPath)!);
            await File.AppendAllTextAsync(
                absoluteLogPath,
                $"{DateTimeOffset.UtcNow:O} {result.TechnicalDetail}{Environment.NewLine}",
                Encoding.UTF8);
        }

        return (int)result.ExitCode;
    }
}
