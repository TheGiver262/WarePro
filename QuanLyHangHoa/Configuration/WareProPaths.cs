using System;
using System.IO;

namespace QuanLyHangHoa.Configuration;

public sealed class WareProPaths
{
    private WareProPaths(string installDirectory, string programDataRoot, string localDataRoot)
    {
        InstallDirectory = Normalize(installDirectory, nameof(installDirectory));
        var programData = Normalize(programDataRoot, nameof(programDataRoot));
        var localData = Normalize(localDataRoot, nameof(localDataRoot));

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

    public static WareProPaths Current => FromRoots(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    public static WareProPaths FromRoots(string installDirectory, string programDataRoot, string localDataRoot) =>
        new(installDirectory, programDataRoot, localDataRoot);

    public void EnsureMachineDirectories()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MachineConfigPath)!);
        Directory.CreateDirectory(InstallerLogDirectory);
    }

    public void EnsureUserDirectories()
    {
        Directory.CreateDirectory(UserLogDirectory);
        Directory.CreateDirectory(UpdateCacheDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(UpdateStatePath)!);
    }


    private static string Normalize(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}
