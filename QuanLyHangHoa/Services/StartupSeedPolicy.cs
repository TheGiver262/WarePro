using System;

namespace QuanLyHangHoa.Services;

/// <summary>
/// tập trung điều kiện seed để fast path và initializer không diễn giải khác nhau.
/// </summary>
public static class StartupSeedPolicy
{
    // không có file thì hàm chỉ trả false; initializer quyết định khi nào phải báo lỗi thiếu file.
    public static bool ShouldSeed(bool seedFileExists, bool hasAnyUsers, bool forceSeed)
    {
        if (!seedFileExists)
        {
            return false;
        }

        // mặc định chỉ seed database chưa có người dùng; forceSeed dành cho thao tác hỗ trợ có chủ đích.
        return forceSeed || !hasAnyUsers;
    }

    public static bool CanSkipInitialization(
        int schemaVersion,
        int requiredSchemaVersion,
        bool hasAnyUsers,
        bool forceSeed)
    {
        // chỉ bỏ qua toàn bộ startup khi schema đã đúng, dữ liệu gốc đã có và không yêu cầu seed lại.
        return schemaVersion == requiredSchemaVersion && hasAnyUsers && !forceSeed;
    }

    public static bool IsForceSeedEnabled()
    {
        // chấp nhận ba dạng phổ biến để biến môi trường dễ dùng trong script triển khai.
        var value = Environment.GetEnvironmentVariable("WAREPRO_FORCE_SEED");
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }
}
