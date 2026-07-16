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

        // summary đi ra stdout; technical detail chỉ ghi vào file log khi caller cung cấp đường dẫn.
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

        // bộ cài dựa vào exit code để chọn thông báo hoặc dừng đúng bước.
        return (int)result.ExitCode;
    }
}
