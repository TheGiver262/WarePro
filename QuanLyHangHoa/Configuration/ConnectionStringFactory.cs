using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Configuration;

/// <summary>
/// báo riêng trường hợp chế độ SQL password chưa có credential trong kho Windows.
/// </summary>
public sealed class WareProCredentialException : Exception
{
    public WareProCredentialException()
        : base("CFG-CREDENTIAL-MISSING: SQL credential is not available.")
    {
    }

    public string Code => "CFG-CREDENTIAL-MISSING";
}

/// <summary>
/// dựng connection string từ cấu hình máy mà không để mật khẩu tồn tại lâu trong bộ nhớ thường.
/// </summary>
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

    /// <summary>
    /// nạp cấu hình mặc định và vẫn ưu tiên connection string do môi trường triển khai cung cấp.
    /// </summary>
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

    // từ đây chỉ xử lý cấu hình máy; quyền ghi đè của môi trường đã được giải quyết ở hai hàm public.
    private string ResolveSettings(WareProSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings.Database);
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Database.Server,
            InitialCatalog = settings.Database.Database,
            TrustServerCertificate = settings.Database.TrustServerCertificate,
            ApplicationName = "WarePro"
        };

        // Windows authentication dùng danh tính tiến trình nên không đọc kho credential của người dùng.
        if (settings.Database.Authentication == DatabaseAuthentication.Windows)
        {
            builder.IntegratedSecurity = true;
            return builder.ConnectionString;
        }

        // SQL password bắt buộc có credential; thiếu dữ liệu được phân loại trước khi mở kết nối.
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
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // override triển khai vẫn phải có application name ổn định để theo dõi connection trên sql server.
        var builder = new SqlConnectionStringBuilder(value)
        {
            ApplicationName = "WarePro"
        };
        return builder.ConnectionString;
    }
}

/// <summary>
/// lọc thông tin xác thực trước khi chi tiết kỹ thuật được ghi log hoặc đưa vào kết quả startup.
/// </summary>
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
