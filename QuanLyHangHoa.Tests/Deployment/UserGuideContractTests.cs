namespace QuanLyHangHoa.Tests.Deployment;

public class UserGuideContractTests
{
    [Fact]
    public void Windows_install_guide_matches_installer_update_and_data_safety_contracts()
    {
        var root = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var guide = File.ReadAllText(Path.Combine(
            root, "docs", "user-guides", "WAREPRO_HUONG_DAN_CAI_DAT_WINDOWS.md"));

        foreach (var requiredText in new[]
                 {
                     "Cài đầy đủ một-click",
                     "Chỉ cài WarePro",
                     "SQL Authentication",
                     "Windows Credential Manager",
                     "Hệ thống > Cập nhật WarePro",
                     "%ProgramData%\\WarePro\\InstallerLogs",
                     "%LocalAppData%\\WarePro\\Logs",
                     "CFG-CREDENTIAL-MISSING",
                     "không xóa database",
                     "không restore"
                 })
        {
            Assert.Contains(requiredText, guide, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Administrator", guide, StringComparison.Ordinal);
        Assert.Contains("WAREPRO_CONNECTION_STRING", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ConnectionString\"", guide, StringComparison.Ordinal);
    }
}
