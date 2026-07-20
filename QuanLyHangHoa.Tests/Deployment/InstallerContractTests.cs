using System.Text.Json;

namespace QuanLyHangHoa.Tests.Deployment;

public class InstallerContractTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Installer_keeps_fixed_identity_and_x64_machine_scope()
    {
        var script = ReadInstaller();

        Assert.Contains("AppId={{47F3016C-70E3-4BEE-A4AF-6934F7CB7626}", script, StringComparison.Ordinal);
        Assert.Contains("DefaultDirName={autopf64}\\WarePro", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesAllowed=x64compatible", script, StringComparison.Ordinal);
        Assert.Contains("ArchitecturesInstallIn64BitMode=x64compatible", script, StringComparison.Ordinal);
        Assert.Contains("PrivilegesRequired=admin", script, StringComparison.Ordinal);
        Assert.Contains("CloseApplications=yes", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_contains_full_and_app_only_modes_with_helper_gates()
    {
        var script = ReadInstaller() + ReadSqlInclude();

        Assert.Contains("FullMode", script, StringComparison.Ordinal);
        Assert.Contains("AppOnlyMode", script, StringComparison.Ordinal);
        Assert.Contains("detect-sql", script, StringComparison.Ordinal);
        Assert.Contains("write-config", script, StringComparison.Ordinal);
        Assert.Contains("test-connection", script, StringComparison.Ordinal);
        Assert.Contains("--mode full", script, StringComparison.Ordinal);
        Assert.Contains("--mode app-only", script, StringComparison.Ordinal);
        Assert.Contains("SQL2022-SSEI-Expr.exe", script, StringComparison.Ordinal);
        Assert.Contains("/FEATURES=SQLEngine", script, StringComparison.Ordinal);
        Assert.Contains("/INSTANCENAME=SQLEXPRESS", script, StringComparison.Ordinal);
        Assert.Contains("/ADDCURRENTUSERASSQLADMIN=True", script, StringComparison.Ordinal);
        Assert.Contains("[Components]", script, StringComparison.Ordinal);
        Assert.Contains("Types: full app-only", script, StringComparison.Ordinal);
        Assert.Contains("Types: full", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Full_mode_checks_existing_SQL_version_and_defers_health_check_after_3010()
    {
        var script = ReadInstaller() + ReadSqlInclude();

        Assert.Contains("SqlInstanceSupported", script, StringComparison.Ordinal);
        Assert.Contains("Version", script, StringComparison.Ordinal);
        Assert.Contains("Edition", script, StringComparison.Ordinal);
        Assert.Contains("SaveConfigurationForRestart", script, StringComparison.Ordinal);
        Assert.Contains("if SqlRestartRequired then", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(
            new System.Text.RegularExpressions.Regex(@"(?m)^\s*\[ExitCode\]"), script);
    }

    [Fact]
    public void Upgrade_mode_requires_existing_config_and_never_enters_full_sql_mode()
    {
        var script = ReadInstaller();

        Assert.Contains("UpgradeMode", script, StringComparison.Ordinal);
        Assert.Contains("WAREPROMODE", script, StringComparison.Ordinal);
        Assert.Contains("PreviousInstallExists", script, StringComparison.Ordinal);
        Assert.Contains("(not UpgradeMode) and", script, StringComparison.Ordinal);
        Assert.Contains("if UpgradeMode or ResumeFullMode then", script, StringComparison.Ordinal);
        Assert.Contains("if not FileExists(FinalConfig) then", script, StringComparison.Ordinal);
        Assert.Contains("ConfigToTest := FinalConfig", script, StringComparison.Ordinal);
        Assert.Contains("PageID = wpSelectComponents", script, StringComparison.Ordinal);
    }
    [Fact]
    public void App_only_mode_never_runs_or_downloads_sql()
    {
        var sqlInclude = File.ReadAllText(Path.Combine(
            Root, "installer", "includes", "SqlExpress2022.iss"));

        Assert.Contains("IsFullMode", sqlInclude, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAppOnlyMode then Download", sqlInclude, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IsAppOnlyMode then Exec", sqlInclude, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Uninstall_never_removes_database_credentials_or_sql_instance()
    {
        var script = ReadInstaller();
        var forbidden = new[]
        {
            "DROP DATABASE",
            "/ACTION=Uninstall",
            "MSSQL$SQLEXPRESS",
            "CredDelete",
            "CredentialTarget"
        };

        foreach (var value in forbidden)
        {
            Assert.DoesNotContain(value, script, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("keep database", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keep credentials", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void App_only_sql_authentication_requires_pre_provisioned_credential_before_upgrade()
    {
        var script = ReadInstaller();

        Assert.Contains("credential must already exist in Windows Credential Manager", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TestConfiguration(ConfigToTest, '--mode app-only'", script, StringComparison.Ordinal);
        Assert.Contains("'prepare-database --config ' + AddQuotes(FinalConfig)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("first-run credential", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--username", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--password", script, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Installer_packages_seed_helper_and_machine_log_path()
    {
        var script = ReadInstaller();

        Assert.Contains("Database\\warepro_database_seed.xlsx", script, StringComparison.Ordinal);
        Assert.Contains("WarePro.SetupHelper.exe", script, StringComparison.Ordinal);
        Assert.Contains("{commonappdata}\\WarePro\\InstallerLogs", script, StringComparison.Ordinal);
        Assert.Contains("desktopicon", script, StringComparison.Ordinal);
        Assert.Contains("{group}\\WarePro", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_dependency_is_locked_to_verified_official_artifact()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            Root, "installer", "dependencies.json")));
        var sql = document.RootElement.GetProperty("sqlServer2022Express");

        Assert.Equal("16.0.1000.6", sql.GetProperty("version").GetString());
        Assert.StartsWith("https://download.microsoft.com/", sql.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            "36E0EC2AC3DD60F496C99CE44722C629209EA7302A2CE9CBFD1E42A73510D7B6",
            sql.GetProperty("sha256").GetString());
        Assert.Equal("Microsoft Corporation", sql.GetProperty("publisher").GetString());
        Assert.Equal(4290992, sql.GetProperty("size").GetInt64());
    }

    [Fact]
    public void Installer_smoke_script_covers_lifecycle_evidence_and_safety()
    {
        var script = File.ReadAllText(Path.Combine(
            Root, "installer", "tests", "Invoke-WareProInstallerSmoke.ps1"));

        foreach (var parameter in new[]
                 {
                     "InstallerPath", "Mode", "SqlServer", "Database",
                     "ExpectedVersion", "LogDirectory", "Authentication"
                 })
        {
            Assert.Contains("$" + parameter, script, StringComparison.Ordinal);
        }

        Assert.Contains("Get-AuthenticodeSignature", script, StringComparison.Ordinal);
        Assert.Contains("/VERYSILENT", script, StringComparison.Ordinal);
        Assert.Contains("/RESTARTEXITCODE=3010", script, StringComparison.Ordinal);
        Assert.Contains("WarePro.exe", script, StringComparison.Ordinal);
        Assert.Contains("warepro.settings.json", script, StringComparison.Ordinal);
        Assert.Contains("schemaVersion", script, StringComparison.Ordinal);
        Assert.Contains("*.lnk", script, StringComparison.Ordinal);
        Assert.Contains("Password", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SQLEXPRESS", script, StringComparison.Ordinal);
        Assert.Contains("Uninstall", script, StringComparison.Ordinal);
        Assert.Contains("PSCredential", script, StringComparison.Ordinal);
        Assert.Contains("CredWrite", script, StringComparison.Ordinal);
        Assert.Contains("WarePro.SetupHelper.exe", script, StringComparison.Ordinal);
        Assert.Contains("test-connection", script, StringComparison.Ordinal);
        Assert.Contains("MainWindowHandle", script, StringComparison.Ordinal);
        Assert.Contains("/WAREPROMODE=upgrade", script, StringComparison.Ordinal);
        Assert.Contains("evidence-", script, StringComparison.Ordinal);
        Assert.Contains("PendingFullInstall", script, StringComparison.Ordinal);
        Assert.Contains("RestartRequiredRerunInstaller", script, StringComparison.Ordinal);
        Assert.Contains("command=prepare-database exit=0", script, StringComparison.Ordinal);
        Assert.Contains("command=finalize-database exit=0", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DeferredUntilRestart", script, StringComparison.Ordinal);
        Assert.DoesNotContain("'evidence.json'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP DATABASE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delete SQL", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fresh_full_install_does_not_reuse_a_stale_machine_config()
    {
        var script = ReadInstaller();

        Assert.Contains("if UpgradeMode or ResumeFullMode then", script, StringComparison.Ordinal);
        Assert.Contains("ConfigToTest := FinalConfig", script, StringComparison.Ordinal);
        Assert.Contains("ConfigToTest := StagingConfig", script, StringComparison.Ordinal);
        Assert.Contains("WriteConfiguration(StagingConfig", script, StringComparison.Ordinal);
        Assert.DoesNotContain("IsFullMode and FileExists(FinalConfig)", script, StringComparison.Ordinal);
    }    [Fact]
    public void Database_cutover_commands_use_single_release_defines()
    {
        var script = ReadInstaller();

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, @"#define\s+MyAppVersion\b"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(script, @"#define\s+MySchemaRelease\b"));
        Assert.DoesNotContain("--app-version 1.1.0", script, StringComparison.Ordinal);
        Assert.Matches(@"#define\s+MySchemaRelease\s+7\b", script);
        Assert.DoesNotContain("--expected-schema 6", script, StringComparison.Ordinal);
        Assert.Contains("--app-version {#MyAppVersion} --expected-schema {#MySchemaRelease}", script, StringComparison.Ordinal);
    }
    private static string ReadInstaller() => File.ReadAllText(Path.Combine(
        Root, "installer", "WarePro.iss"));

    private static string ReadSqlInclude() => File.ReadAllText(Path.Combine(
        Root, "installer", "includes", "SqlExpress2022.iss"));
}
