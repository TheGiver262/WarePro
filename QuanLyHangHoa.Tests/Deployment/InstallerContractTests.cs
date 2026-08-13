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
    public void Installer_contains_server_client_and_standalone_roles_with_helper_gates()
    {
        var script = ReadInstaller() + ReadSqlInclude();

        Assert.Contains("ServerMode", script, StringComparison.Ordinal);
        Assert.Contains("ClientMode", script, StringComparison.Ordinal);
        Assert.Contains("StandaloneMode", script, StringComparison.Ordinal);
        Assert.Contains("FullMode", script, StringComparison.Ordinal);
        Assert.Contains("AppOnlyMode", script, StringComparison.Ordinal);
        Assert.Contains("RequestedSetupType", script, StringComparison.Ordinal);
        Assert.Contains("detect-sql", script, StringComparison.Ordinal);
        Assert.Contains("write-config", script, StringComparison.Ordinal);
        Assert.Contains("WAREPROENCRYPT", script, StringComparison.Ordinal);
        Assert.Contains("ParameterOrDefault('WAREPROENCRYPT', 'false')", script, StringComparison.Ordinal);
        Assert.Contains("test-connection", script, StringComparison.Ordinal);
        Assert.Contains("SQL2022-SSEI-Expr.exe", script, StringComparison.Ordinal);
        Assert.Contains("/FEATURES=SQLEngine", script, StringComparison.Ordinal);
        Assert.Contains("/INSTANCENAME=SQLEXPRESS", script, StringComparison.Ordinal);
        Assert.Contains("/ADDCURRENTUSERASSQLADMIN=True", script, StringComparison.Ordinal);
        Assert.Contains("[Components]", script, StringComparison.Ordinal);
        Assert.Contains("Types: server client standalone", script, StringComparison.Ordinal);
        Assert.Contains("Types: server standalone", script, StringComparison.Ordinal);
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
    public void Upgrade_mode_defaults_to_client_and_requires_existing_config()
    {
        var script = ReadInstaller();

        Assert.Contains("UpgradeMode", script, StringComparison.Ordinal);
        Assert.Contains("WAREPROMODE", script, StringComparison.Ordinal);
        Assert.Contains("PreviousInstallExists", script, StringComparison.Ordinal);
        Assert.Contains("RequestedType := ClientMode", script, StringComparison.Ordinal);
        Assert.Contains("if UpgradeMode then", script, StringComparison.Ordinal);
        Assert.Contains("if not FileExists(FinalConfig) then", script, StringComparison.Ordinal);
        Assert.Contains("PrepareClientInstall", script, StringComparison.Ordinal);
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
    public void Client_sql_password_setup_defers_credential_probe_to_first_launch()
    {
        var script = ReadInstaller();
        var clientStart = script.IndexOf("procedure PrepareClientInstall", StringComparison.Ordinal);
        var clientEnd = script.IndexOf("function PrepareToInstall", clientStart, StringComparison.Ordinal);
        var clientFlow = script[clientStart..clientEnd];

        Assert.Contains("CompareText(SelectedAuthentication, 'SqlPassword') = 0", clientFlow, StringComparison.Ordinal);
        Assert.Contains("TestConfiguration(StagingConfig, '--mode app-only'", clientFlow, StringComparison.Ordinal);
        Assert.Contains("lần mở WarePro đầu tiên", clientFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("credential must already exist in Windows Credential Manager", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--username", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("--password", script, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Client_install_writes_and_tests_configuration_without_database_cutover()
    {
        var installer = ReadInstaller();
        var clientStart = installer.IndexOf("procedure PrepareClientInstall", StringComparison.Ordinal);
        var clientEnd = installer.IndexOf("function PrepareToInstall", clientStart, StringComparison.Ordinal);
        var clientFlow = installer[clientStart..clientEnd];

        Assert.Contains("WriteConfiguration(StagingConfig", clientFlow, StringComparison.Ordinal);
        Assert.Contains("TestConfiguration(StagingConfig, '--mode app-only'", clientFlow, StringComparison.Ordinal);
        Assert.Contains("WriteConfiguration(FinalConfig", clientFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareDatabaseCutover", clientFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalizeDatabaseCutover", clientFlow, StringComparison.Ordinal);
        Assert.DoesNotContain("RollbackDatabaseCutover", clientFlow, StringComparison.Ordinal);
    }
    [Fact]
    public void Installer_declares_roles_and_keeps_client_out_of_database_provisioning()
    {
        var script = ReadInstaller();

        Assert.Contains("Name: \"server\"", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"client\"", script, StringComparison.Ordinal);
        Assert.Contains("Name: \"standalone\"", script, StringComparison.Ordinal);
        Assert.Contains("function IsServerRole", script, StringComparison.Ordinal);
        Assert.Contains("function IsClientRole", script, StringComparison.Ordinal);
        Assert.Contains("function IsStandaloneRole", script, StringComparison.Ordinal);
        Assert.Contains("function ShouldProvisionDatabase", script, StringComparison.Ordinal);
        Assert.Contains("Result := IsServerRole or IsStandaloneRole;", script, StringComparison.Ordinal);

        var installFlow = script[script.IndexOf("function PrepareToInstall", StringComparison.Ordinal)..];
        Assert.Contains("if ShouldProvisionDatabase then", installFlow, StringComparison.Ordinal);
        Assert.Contains("EnsureSqlExpress", installFlow, StringComparison.Ordinal);
        Assert.Contains("PrepareDatabaseCutover;", installFlow, StringComparison.Ordinal);
        Assert.Contains("configure-lan", script, StringComparison.Ordinal);
        Assert.Contains("LocalSubnet", script, StringComparison.Ordinal);
        Assert.Contains("/TCPENABLED=1", ReadSqlInclude(), StringComparison.Ordinal);
        Assert.Contains("/NPENABLED=0", ReadSqlInclude(), StringComparison.Ordinal);
    }
    [Fact]
    public void Installer_activates_the_LAN_endpoint_only_after_database_finalization()
    {
        var script = ReadInstaller();
        var prepareStart = script.IndexOf("function PrepareToInstall", StringComparison.Ordinal);
        var prepareEnd = script.IndexOf("procedure SaveConfigurationForRestart", prepareStart, StringComparison.Ordinal);
        var prepare = script[prepareStart..prepareEnd];
        var postInstallStart = script.IndexOf("else if DatabasePrepared then", StringComparison.Ordinal);
        var postInstallEnd = script.IndexOf("else if IsClientRole", postInstallStart, StringComparison.Ordinal);
        var postInstall = script[postInstallStart..postInstallEnd];

        Assert.DoesNotContain("ConfigureLanEndpoint;", prepare, StringComparison.Ordinal);
        Assert.True(postInstall.IndexOf("FinalizeDatabaseCutover", StringComparison.Ordinal) >= 0);
        Assert.True(
            postInstall.IndexOf("DatabaseFinalized := True", StringComparison.Ordinal)
            < postInstall.IndexOf("ConfigureLanEndpoint;", StringComparison.Ordinal));
    }

    [Fact]
    public void Installer_defines_lan_resume_state_before_writing_it()
    {
        var script = ReadInstaller();

        var writer = script.IndexOf("procedure WritePendingFullInstall", StringComparison.Ordinal);
        var definition = script.IndexOf(
            "function ShouldEnableLan: Boolean;" + Environment.NewLine + "begin",
            StringComparison.Ordinal);

        Assert.True(writer >= 0);
        Assert.True(definition >= 0);
        Assert.True(definition < writer);
        Assert.DoesNotContain(
            "function ShouldEnableLan: Boolean; forward;",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_defines_lan_port_before_writing_resume_state()
    {
        var script = ReadInstaller();

        var writer = script.IndexOf("procedure WritePendingFullInstall", StringComparison.Ordinal);
        var definition = script.IndexOf(
            "function SelectedLanPort: Integer;" + Environment.NewLine + "begin",
            StringComparison.Ordinal);

        Assert.True(writer >= 0);
        Assert.True(definition >= 0);
        Assert.True(definition < writer);
        Assert.DoesNotContain(
            "function SelectedLanPort: Integer; forward;",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_terminates_database_probe_before_promoting_config()
    {
        var script = ReadInstaller();

        Assert.Matches(
            new System.Text.RegularExpressions.Regex(
                @"(?s)if ShouldProvisionDatabase then\s*begin.*?end;\s*if CompareText\(ConfigToTest, StagingConfig\)"),
            script);
    }

    [Fact]
    public void Installer_packages_setup_helper_and_the_complete_app_publish_tree()
    {
        var script = ReadInstaller();

        Assert.Contains("Source: \"{#PublishDir}\\*\"", script, StringComparison.Ordinal);
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
        Assert.Matches(@"#define\s+MySchemaRelease\s+11\b", script);
        Assert.DoesNotContain("--expected-schema 6", script, StringComparison.Ordinal);
        Assert.Contains("--app-version {#MyAppVersion} --expected-schema {#MySchemaRelease}", script, StringComparison.Ordinal);
    }
    private static string ReadInstaller() => File.ReadAllText(Path.Combine(
        Root, "installer", "WarePro.iss"));

    private static string ReadSqlInclude() => File.ReadAllText(Path.Combine(
        Root, "installer", "includes", "SqlExpress2022.iss"));
}
