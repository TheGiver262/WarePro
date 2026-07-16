using System;

namespace QuanLyHangHoa.Startup;

/// <summary>
/// tách thông điệp cho người dùng, chi tiết kỹ thuật đã lọc và vị trí log của một lần startup.
/// </summary>
public sealed record StartupResult(
    bool Success,
    string? ErrorCode,
    string UserMessage,
    string TechnicalDetailRedacted,
    string LogPath)
{
    // kết quả thành công không mang mã lỗi hay chi tiết kỹ thuật cũ.
    public static StartupResult Succeeded(string logPath) =>
        new(true, null, string.Empty, string.Empty, logPath);

    public static StartupResult Failed(
        string errorCode,
        string userMessage,
        string technicalDetailRedacted,
        string logPath) =>
        new(false, errorCode, userMessage, technicalDetailRedacted, logPath);
}

/// <summary>
/// lỗi startup đã được gắn mã ổn định và thông điệp an toàn để hiển thị.
/// </summary>
public sealed class StartupFailureException : Exception
{
    public StartupFailureException(
        string code,
        string userMessage,
        string technicalDetail,
        Exception? innerException = null)
        : base(technicalDetail, innerException)
    {
        Code = code;
        UserMessage = userMessage;
    }

    public string Code { get; }
    public string UserMessage { get; }
}
