using System.IO;
using System.Text.Json;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Tests.Configuration;

public class WareProSettingsTests
{
    [Fact]
    public void Paths_keep_installed_files_machine_config_and_user_state_separate()
    {
        var root = Path.Combine(Path.GetTempPath(), "warepro-path-contract");
        var install = Path.Combine(root, "ProgramFiles", "WarePro");
        var programData = Path.Combine(root, "ProgramData");
        var localData = Path.Combine(root, "LocalAppData");

        var paths = WareProPaths.FromRoots(install, programData, localData);

        Assert.Equal(Path.GetFullPath(install), paths.InstallDirectory);
        Assert.Equal(Path.Combine(programData, "WarePro", "Config", "warepro.settings.json"), paths.MachineConfigPath);
        Assert.Equal(Path.Combine(programData, "WarePro", "InstallerLogs"), paths.InstallerLogDirectory);
        Assert.Equal(Path.Combine(localData, "WarePro", "Logs"), paths.UserLogDirectory);
        Assert.Equal(Path.Combine(localData, "WarePro", "Updates"), paths.UpdateCacheDirectory);
        Assert.Equal(Path.Combine(localData, "WarePro", "State", "update-state.json"), paths.UpdateStatePath);
        Assert.Equal(Path.Combine(install, "Database", "warepro_database_seed.xlsx"), paths.SeedWorkbookPath);
    }

    [Fact]
    public void Default_settings_round_trip_without_storing_database_credentials()
    {
        var settings = WareProSettings.CreateDefault();

        var json = WareProSettingsStore.Serialize(settings);
        var restored = WareProSettingsStore.Deserialize(json);

        Assert.Equal(WareProSettings.CurrentSchemaVersion, restored.SchemaVersion);
        Assert.Equal(@".\SQLEXPRESS", restored.Database.Server);
        Assert.Equal("ProductManagementDb", restored.Database.Database);
        Assert.Equal(DatabaseAuthentication.Windows, restored.Database.Authentication);
        Assert.True(restored.Database.TrustServerCertificate);
        Assert.False(restored.Database.Encrypt);
        Assert.Equal("TheGiver262/WarePro-Releases", restored.Updates.Repository);
        Assert.Equal("stable", restored.Updates.Channel);
        Assert.Equal(24, restored.Updates.CheckIntervalHours);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.False(root.TryGetProperty("Password", out _));
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Encrypt\": false", json, StringComparison.Ordinal);
    }
    [Fact]
    public void Paths_create_only_the_explicit_writable_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "warepro-paths-" + Guid.NewGuid());
        var paths = WareProPaths.FromRoots(
            Path.Combine(root, "ProgramFiles", "WarePro"),
            Path.Combine(root, "ProgramData"),
            Path.Combine(root, "LocalAppData"));

        try
        {
            paths.EnsureMachineDirectories();
            paths.EnsureUserDirectories();

            Assert.True(Directory.Exists(Path.GetDirectoryName(paths.MachineConfigPath)));
            Assert.True(Directory.Exists(paths.InstallerLogDirectory));
            Assert.True(Directory.Exists(paths.UserLogDirectory));
            Assert.True(Directory.Exists(paths.UpdateCacheDirectory));
            Assert.True(Directory.Exists(Path.GetDirectoryName(paths.UpdateStatePath)));
            Assert.False(Directory.Exists(paths.InstallDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Load_returns_null_when_the_machine_config_file_is_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var store = new WareProSettingsStore(path);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_reports_invalid_json_without_overwriting_the_bad_file()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        const string invalidJson = "{ this is not json";
        File.WriteAllText(path, invalidJson);

        try
        {
            var error = Assert.Throws<WareProConfigurationException>(
                () => new WareProSettingsStore(path).Load());

            Assert.Equal("CFG-CONFIG-INVALID", error.Code);
            Assert.Equal(Path.GetFullPath(path), error.ConfigurationPath);
            Assert.Equal(invalidJson, File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_rejects_an_unsupported_schema_version()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        File.WriteAllText(path, """{"SchemaVersion":2}""");

        try
        {
            var error = Assert.Throws<WareProConfigurationException>(
                () => new WareProSettingsStore(path).Load());

            Assert.Equal("CFG-CONFIG-INVALID", error.Code);
            Assert.Equal(Path.GetFullPath(path), error.ConfigurationPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Save_replaces_the_config_without_leaving_a_partial_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "warepro-settings-" + Guid.NewGuid());
        var path = Path.Combine(root, "Config", "warepro.settings.json");
        var store = new WareProSettingsStore(path);

        try
        {
            store.Save(WareProSettings.CreateDefault());
            var changed = WareProSettings.CreateDefault();
            changed.Updates.Channel = "preview";
            store.Save(changed);

            Assert.Equal("preview", store.Load()!.Updates.Channel);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

}
