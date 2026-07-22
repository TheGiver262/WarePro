namespace QuanLyHangHoa.Configuration;

/// <summary>
/// cách SQL Server xác thực kết nối của ứng dụng.
/// </summary>
public enum DatabaseAuthentication
{
    Windows,
    SqlPassword
}

/// <summary>
/// thông số kết nối dùng chung; mật khẩu SQL không được lưu trong file này.
/// </summary>
public sealed class WareProDatabaseSettings
{
    public string Server { get; set; } = @".\SQLEXPRESS";
    public string Database { get; set; } = "ProductManagementDb";
    public DatabaseAuthentication Authentication { get; set; } = DatabaseAuthentication.Windows;
    public bool TrustServerCertificate { get; set; } = true;
    public bool Encrypt { get; set; } = false;
}

/// <summary>
/// nguồn phát hành, kênh cập nhật và chu kỳ kiểm tra phiên bản mới.
/// </summary>
public sealed class WareProUpdateSettings
{
    public string Repository { get; set; } = "TheGiver262/WarePro-Releases";
    // channel tách bản ổn định và bản thử nghiệm mà không cần đổi chương trình.
    public string Channel { get; set; } = "stable";
    // đơn vị giờ giúp file JSON dễ đọc; trình cập nhật sẽ chuyển thành khoảng thời gian khi chạy.
    public int CheckIntervalHours { get; set; } = 24;
}

/// <summary>
/// mô hình file cấu hình máy do bộ cài hoặc quản trị viên duy trì.
/// </summary>
public sealed class WareProSettings
{
    public const int CurrentSchemaVersion = 1;

    // đây là phiên bản cấu trúc JSON, không phải phiên bản schema của cơ sở dữ liệu.
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public WareProDatabaseSettings Database { get; set; } = new();
    public WareProUpdateSettings Updates { get; set; } = new();

    public static WareProSettings CreateDefault() => new();
}
