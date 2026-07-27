using System;

namespace QuanLyHangHoa.Services;

/// <summary>
/// quan hệ giữa schema database và khả năng đọc/nâng cấp của client hiện tại.
/// </summary>
public enum DatabaseCompatibilityStatus
{
    Compatible,
    UpgradeRequired,
    ClientUpdateRequired
}

public sealed record DatabaseCompatibilityResult(
    DatabaseCompatibilityStatus Status,
    int DatabaseSchemaVersion,
    int CurrentSchemaVersion);

/// <summary>
/// chặn client cũ mở database mới hơn để tránh ghi dữ liệu bằng mô hình không tương thích.
/// </summary>
public sealed class DatabaseCompatibilityException : Exception
{
    public DatabaseCompatibilityException(
        int databaseSchemaVersion,
        string? minimumClientVersion = null,
        string? currentClientVersion = null)
        : base($"DB-CLIENT-UPDATE-REQUIRED: Database schema {databaseSchemaVersion} requires a newer WarePro version." +
            $" Minimum client: {minimumClientVersion ?? "unknown"}; current client: {currentClientVersion ?? "unknown"}.")
    {
        Code = "DB-CLIENT-UPDATE-REQUIRED";
        DatabaseSchemaVersion = databaseSchemaVersion;
        MinimumClientVersion = minimumClientVersion;
        CurrentClientVersion = currentClientVersion;
    }

    public string Code { get; }
    public int DatabaseSchemaVersion { get; }
    public string? MinimumClientVersion { get; }
    public string? CurrentClientVersion { get; }
}

/// <summary>
/// quyết định client có thể dùng, phải nâng schema hay phải tự cập nhật trước.
/// </summary>
public sealed class DatabaseCompatibilityService
{
    public const int CurrentSchemaVersion = 9;
    public const int MinimumSupportedSchemaVersion = 9;
    public const int MaximumSupportedSchemaVersion = 9;

    public DatabaseCompatibilityResult Evaluate(int databaseSchemaVersion)
    {
        // schema cũ hơn mức hỗ trợ yêu cầu installer nâng database trước khi client tiếp tục.
        if (databaseSchemaVersion < MinimumSupportedSchemaVersion)
        {
            return new DatabaseCompatibilityResult(
                DatabaseCompatibilityStatus.UpgradeRequired,
                databaseSchemaVersion,
                CurrentSchemaVersion);
        }

        // schema mới hơn client là nhánh nguy hiểm và phải dừng để cập nhật ứng dụng.
        if (databaseSchemaVersion > MaximumSupportedSchemaVersion)
        {
            return new DatabaseCompatibilityResult(
                DatabaseCompatibilityStatus.ClientUpdateRequired,
                databaseSchemaVersion,
                CurrentSchemaVersion);
        }

        return new DatabaseCompatibilityResult(
            DatabaseCompatibilityStatus.Compatible,
            databaseSchemaVersion,
            CurrentSchemaVersion);
    }

    public DatabaseCompatibilityResult Evaluate(
        int databaseSchemaVersion,
        string? minimumClientVersion,
        string? currentClientVersion)
    {
        var schemaResult = Evaluate(databaseSchemaVersion);
        if (schemaResult.Status == DatabaseCompatibilityStatus.ClientUpdateRequired)
        {
            return schemaResult;
        }

        // ngoài schema, metadata database còn có thể yêu cầu một phiên bản client tối thiểu.
        var minimum = ParseVersion(minimumClientVersion);
        var current = ParseVersion(currentClientVersion);
        // không phân tích được version cũng chọn hướng an toàn: yêu cầu client mới thay vì tiếp tục ghi dữ liệu.
        if (minimum is null || current is null || current < minimum)
        {
            return schemaResult with { Status = DatabaseCompatibilityStatus.ClientUpdateRequired };
        }

        return schemaResult;
    }

    // chỉ backup khi vừa có dữ liệu nghiệp vụ vừa thực sự sắp đổi schema.
    public bool RequiresBackup(int databaseSchemaVersion, bool hasExistingBusinessData) =>
        hasExistingBusinessData && databaseSchemaVersion < CurrentSchemaVersion;

    // bỏ nhãn prerelease và build metadata vì System.Version chỉ nhận phần số.
    private static Version? ParseVersion(string? value)
    {
        var numericPart = value?.Split(['-', '+'], 2)[0];
        return Version.TryParse(numericPart, out var parsed) ? parsed : null;
    }
}
