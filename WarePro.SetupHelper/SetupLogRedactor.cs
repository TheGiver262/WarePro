using QuanLyHangHoa.Configuration;

namespace WarePro.SetupHelper;

public static class SetupLogRedactor
{
    // dùng chung bộ lọc của ứng dụng để helper và client không có hai quy tắc che credential khác nhau.
    public static string Redact(string? detail) => SensitiveDataRedactor.Redact(detail);
}