using Microsoft.Data.SqlClient;

namespace QuanLyHangHoa.Configuration;

/// <summary>
/// quyết định có cần hỏi credential SQL ở lần chạy đầu hoặc sau khi SQL từ chối tài khoản cũ.
/// </summary>
public sealed class FirstRunCredentialCoordinator
{
    private readonly Func<WareProSettings?> _settingsLoader;
    private readonly ISqlCredentialStore _credentialStore;
    private readonly Func<string?> _environmentReader;

    public FirstRunCredentialCoordinator(
        Func<WareProSettings?> settingsLoader,
        ISqlCredentialStore credentialStore,
        Func<string?> environmentReader)
    {
        _settingsLoader = settingsLoader ?? throw new ArgumentNullException(nameof(settingsLoader));
        _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
    }

    public static FirstRunCredentialCoordinator CreateDefault() => new(
        () => new WareProSettingsStore().Load(),
        new SqlCredentialStore(),
        () => Environment.GetEnvironmentVariable("WAREPRO_CONNECTION_STRING"));

    public bool EnsureCredential(
        Func<SqlCredential?> credentialPrompt,
        bool replaceExisting = false)
    {
        ArgumentNullException.ThrowIfNull(credentialPrompt);

        // connection string từ môi trường đã chứa cách xác thực nên không tạo thêm credential cục bộ.
        if (!string.IsNullOrWhiteSpace(_environmentReader()))
        {
            return true;
        }

        // Windows authentication dùng danh tính tiến trình; file cấu hình chưa có cũng chưa cần hỏi mật khẩu.
        var settings = _settingsLoader();
        if (settings is null || settings.Database.Authentication == DatabaseAuthentication.Windows)
        {
            return true;
        }

        // chỉ đọc credential cũ khi không có yêu cầu thay thế sau lỗi đăng nhập SQL.
        if (!replaceExisting)
        {
            var existingCredential = _credentialStore.Read();
            if (existingCredential is not null)
            {
                existingCredential.Password.Dispose();
                return true;
            }
        }

        // null nghĩa là người dùng chủ động hủy; caller sẽ dừng startup mà không coi đây là lỗi lưu trữ.
        var enteredCredential = credentialPrompt();
        if (enteredCredential is null)
        {
            return false;
        }

        // store sao chép credential vào Windows Credential Manager trước khi mật khẩu đầu vào bị dispose.
        try
        {
            _credentialStore.Write(enteredCredential);
            return true;
        }
        finally
        {
            // luôn xóa vùng nhớ nhạy cảm do hộp thoại tạo ra, kể cả khi lệnh ghi thất bại.
            enteredCredential.Password.Dispose();
        }
    }
}
