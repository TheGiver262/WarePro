using System;

namespace QuanLyHangHoa.Startup;

public sealed record StartupResult(
    bool Success,
    string? ErrorCode,
    string UserMessage,
    string TechnicalDetailRedacted,
    string LogPath)
{
    public static StartupResult Succeeded(string logPath) =>
        new(true, null, string.Empty, string.Empty, logPath);

    public static StartupResult Failed(
        string errorCode,
        string userMessage,
        string technicalDetailRedacted,
        string logPath) =>
        new(false, errorCode, userMessage, technicalDetailRedacted, logPath);
}

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
