using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Configuration;

public sealed class WareProCredentialException : Exception
{
    public WareProCredentialException()
        : base("CFG-CREDENTIAL-MISSING: SQL credential is not available.")
    {
    }

    public string Code => "CFG-CREDENTIAL-MISSING";
}

public sealed class ConnectionStringFactory
{
    private readonly ISqlCredentialStore _credentialStore;
    private readonly Func<string?> _environmentReader;
    private readonly Func<WareProSettings?>? _settingsLoader;

    public ConnectionStringFactory(
        ISqlCredentialStore credentialStore,
        Func<string?> environmentReader,
        Func<WareProSettings?>? settingsLoader = null)
    {
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
        _settingsLoader = settingsLoader;
    }

    public static ConnectionStringFactory CreateDefault() => new(
        new SqlCredentialStore(),
        () => Environment.GetEnvironmentVariable("WAREPRO_CONNECTION_STRING"),
        () => new WareProSettingsStore().Load());

    public string Resolve()
    {
        // biến môi trường được ưu tiên để CI và máy hỗ trợ có thể kết nối tạm
        // mà không phải sửa cấu hình dùng chung trên máy.
        var environmentValue = ReadEnvironmentOverride();
        if (environmentValue is not null)
        {
            return environmentValue;
        }

        return ResolveSettings(_settingsLoader?.Invoke() ?? WareProSettings.CreateDefault());
    }

    public string Resolve(WareProSettings? settings)
    {
        var environmentValue = ReadEnvironmentOverride();
        if (environmentValue is not null)
        {
            return environmentValue;
        }

        return ResolveSettings(settings ?? WareProSettings.CreateDefault());
    }

    private string ResolveSettings(WareProSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.Database);
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Database.Server,
            InitialCatalog = settings.Database.Database,
            TrustServerCertificate = settings.Database.TrustServerCertificate
        };

        if (settings.Database.Authentication == DatabaseAuthentication.Windows)
        {
            builder.IntegratedSecurity = true;
            return builder.ConnectionString;
        }

        var credential = _credentialStore.Read() ?? throw new WareProCredentialException();
        // SecureString chỉ được mở đúng lúc SqlClient cần mật khẩu và luôn bị xóa trong finally.
        // cách này tránh giữ mật khẩu dạng chuỗi thường lâu hơn mức cần thiết.
        builder.IntegratedSecurity = false;
        builder.UserID = credential.UserId;

        var passwordPointer = Marshal.SecureStringToGlobalAllocUnicode(credential.Password);
        try
        {
            builder.Password = Marshal.PtrToStringUni(passwordPointer) ?? string.Empty;
            return builder.ConnectionString;
        }
        finally
        {
            Marshal.ZeroFreeGlobalAllocUnicode(passwordPointer);
        }
    }

    private string? ReadEnvironmentOverride()
    {
        var value = _environmentReader();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

public static partial class SensitiveDataRedactor
{
    // giữ lại tên khóa để log còn dễ đọc, chỉ thay phần giá trị nhạy cảm.
    [GeneratedRegex(@"(?<key>password|pwd|user\s*id|uid|token|credential)\s*=\s*(?:""[^""]*""|'[^']*'|[^;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionSecretPattern();

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        return ConnectionSecretPattern().Replace(value, match => $"{match.Groups["key"].Value}=***");
    }
}
