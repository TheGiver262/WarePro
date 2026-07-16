using System;
using System.IO;

namespace QuanLyHangHoa.Configuration;

/// <summary>
/// gom quy ước đường dẫn để ứng dụng, bộ cài và trình cập nhật dùng cùng vị trí dữ liệu.
/// </summary>
public sealed class WareProPaths
{
    private WareProPaths(string installDirectory, string programDataRoot, string localDataRoot)
    {
        InstallDirectory = Normalize(installDirectory, nameof(installDirectory));
        var programData = Normalize(programDataRoot, nameof(programDataRoot));
        var localData = Normalize(localDataRoot, nameof(localDataRoot));

        // ProgramData chứa cấu hình và log cấp máy; LocalAppData chứa log, cache và state của từng người dùng.
        MachineConfigPath = Path.Combine(programData, "WarePro", "Config", "warepro.settings.json");
        InstallerLogDirectory = Path.Combine(programData, "WarePro", "InstallerLogs");
        UserLogDirectory = Path.Combine(localData, "WarePro", "Logs");
        UpdateCacheDirectory = Path.Combine(localData, "WarePro", "Updates");
        UpdateStatePath = Path.Combine(localData, "WarePro", "State", "update-state.json");
        SeedWorkbookPath = Path.Combine(InstallDirectory, "Database", "warepro_database_seed.xlsx");
    }

    public string InstallDirectory { get; }
    public string MachineConfigPath { get; }
    public string InstallerLogDirectory { get; }
    public string UserLogDirectory { get; }
    public string UpdateCacheDirectory { get; }
    public string UpdateStatePath { get; }
    public string SeedWorkbookPath { get; }

    // Current lấy các thư mục chuẩn của Windows; FromRoots cho phép kiểm thử bằng thư mục tạm.
    public static WareProPaths Current => FromRoots(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static WareProPaths FromRoots(string installDirectory, string programDataRoot, string localDataRoot) =>
        new(installDirectory, programDataRoot, localDataRoot);

    /// <summary>
    /// tạo các thư mục dùng chung, thường được chuẩn bị bởi bộ cài có quyền phù hợp.
    /// </summary>
    public void EnsureMachineDirectories()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MachineConfigPath)!);
        Directory.CreateDirectory(InstallerLogDirectory);
    }

    /// <summary>
    /// tạo các thư mục riêng của tài khoản đang chạy mà không cần quyền quản trị.
    /// </summary>
    public void EnsureUserDirectories()
    {
        Directory.CreateDirectory(UserLogDirectory);
        Directory.CreateDirectory(UpdateCacheDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(UpdateStatePath)!);
    }


    // chuẩn hóa một lần để mọi đường dẫn con đều là đường dẫn tuyệt đối và nhất quán.
    private static string Normalize(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}
