using QuanLyHangHoa.Configuration;

namespace WarePro.SetupHelper;

public static class SetupLogRedactor
{
    public static string Redact(string? detail) => SensitiveDataRedactor.Redact(detail);
}