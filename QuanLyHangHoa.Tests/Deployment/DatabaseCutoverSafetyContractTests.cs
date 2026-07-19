using WarePro.Database;

namespace QuanLyHangHoa.Tests.Deployment;

public sealed class DatabaseCutoverSafetyContractTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Database_preflight_runs_from_temp_before_application_files_are_installed()
    {
        var script = Read("installer", "WarePro.iss");
        var prepare = script[script.IndexOf("function PrepareToInstall", StringComparison.Ordinal)..
            script.IndexOf("procedure CurStepChanged", StringComparison.Ordinal)];
        var postInstall = script[script.IndexOf("procedure CurStepChanged", StringComparison.Ordinal)..];

        Assert.Contains("Flags: dontcopy", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ExtractTemporaryFiles", prepare, StringComparison.Ordinal);
        Assert.Contains("PrepareDatabaseCutover", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("UpgradeDatabase", postInstall, StringComparison.Ordinal);
        Assert.Contains("FinalizeDatabaseCutover", postInstall, StringComparison.Ordinal);
    }

    [Fact]
    public void Installer_exposes_rollback_and_always_supplies_a_machine_log()
    {
        var script = Read("installer", "WarePro.iss");

        Assert.Contains("RollbackDatabaseCutover", script, StringComparison.Ordinal);
        Assert.Contains("Arguments + ' --log ' + AddQuotes(HelperLogPath)", script, StringComparison.Ordinal);
        Assert.Contains("prepare-database", script, StringComparison.Ordinal);
        Assert.Contains("finalize-database", script, StringComparison.Ordinal);
        Assert.Contains("rollback-database", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Baseline_has_no_public_accounts_and_contains_complete_shared_schema()
    {
        var sql = string.Join(Environment.NewLine, DatabaseSchemaScripts.BaselineBatches);

        Assert.DoesNotContain("INSERT INTO dbo.AppUser", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("admin123", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CREATE TABLE dbo.AuditArchiveManifest", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UX_AuditArchiveManifest_OperationId", sql, StringComparison.Ordinal);
        Assert.Contains("FK_StockTransfer_FromWarehouse", sql, StringComparison.Ordinal);
        Assert.Contains("FK_StockTransferLine_StockTransfer", sql, StringComparison.Ordinal);
        Assert.Contains("UX_StockTransfer_DocumentCode", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Preparation_does_not_raise_minimum_client_until_finalize()
    {
        var prepareSql = DatabaseSchemaScripts.BuildUpgradeSql(6, "1.1.0");
        var finalizeSql = DatabaseSchemaScripts.BuildFinalizeSql(6, "1.1.0");

        Assert.DoesNotContain("MinimumClientVersion] = N'1.1.0'", prepareSql, StringComparison.Ordinal);
        Assert.Contains("MinimumClientVersion = N'1.1.0'", finalizeSql, StringComparison.Ordinal);
        Assert.Contains("Version = 6", finalizeSql, StringComparison.Ordinal);
    }

    [Fact]
    public void Runner_validates_schema_shape_and_blocks_legacy_sessions_during_cutover()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");

        foreach (var marker in new[]
                 {
                     "AuditArchiveManifest", "UX_AuditArchiveManifest_OperationId",
                     "FK_StockTransfer_FromWarehouse", "FK_StockTransferLine_StockTransfer",
                     "RowVersion", "ToUpperInvariant()", "Product", "Warehouse",
                     "RESTRICTED_USER", "MULTI_USER"
                 })
            Assert.Contains(marker, source, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_write_gate_rejects_legacy_or_unregistered_clients_and_accepts_schema_six()
    {
        var sql = DatabaseSchemaScripts.BuildFinalizeSql(6, "1.1.0");
        var executor = Read("QuanLyHangHoa", "Data", "DatabaseWriteExecutor.cs");

        Assert.Contains("WareProClientSchema", sql, StringComparison.Ordinal);
        Assert.Contains("THROW", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("< 6", sql, StringComparison.Ordinal);
        Assert.Contains("sp_set_session_context", executor, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WareProClientSchema", executor, StringComparison.Ordinal);
        Assert.Contains("DatabaseCompatibilityService.CurrentSchemaVersion", executor, StringComparison.Ordinal);
    }
    [Fact]
    public void Bootstrap_secret_is_acl_restricted_never_passed_as_a_value_and_always_deleted()
    {
        var installer = Read("installer", "WarePro.iss");
        var runner = Read("WarePro.SetupHelper", "SetupCommands.cs");

        Assert.Contains("/inheritance:r", installer, StringComparison.Ordinal);
        Assert.Contains("*S-1-5-18:F", installer, StringComparison.Ordinal);
        Assert.Contains("*S-1-5-32-544:F", installer, StringComparison.Ordinal);
        Assert.Contains("GetUserNameString", installer, StringComparison.Ordinal);
        Assert.Contains("--bootstrap-secret-file", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("Arguments + BootstrapPage.Values[0]", installer, StringComparison.Ordinal);
        Assert.Contains("ReadAllTextAsync(bootstrapSecretFile", runner, StringComparison.Ordinal);
        Assert.Contains("BCrypt.Net.BCrypt.HashPassword", runner, StringComparison.Ordinal);
        Assert.Contains("finally", runner, StringComparison.Ordinal);
        Assert.Contains("File.Delete(bootstrapSecretFile)", runner, StringComparison.Ordinal);
        Assert.Contains("MustChangePassword", runner, StringComparison.Ordinal);
    }

    [Fact]
    public void Upgrade_commands_split_prepare_finalize_and_rollback()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");

        Assert.Contains("prepare-database", source, StringComparison.Ordinal);
        Assert.Contains("finalize-database", source, StringComparison.Ordinal);
        Assert.Contains("rollback-database", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_database_detection_requires_the_known_warepro_relational_shape()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var start = source.IndexOf("ClassifyDatabaseAsync", StringComparison.Ordinal);
        var detection = source[start..source.IndexOf("private static void ValidateRelease", start, StringComparison.Ordinal)];

        Assert.Contains("@DistinctiveLegacyShape", detection, StringComparison.Ordinal);
        Assert.Contains("CK_ProductUnit_ConversionFactor_Positive", detection, StringComparison.Ordinal);
        Assert.Contains("FK_StockInLine_StockIn", detection, StringComparison.Ordinal);
        Assert.Contains("FK_StockOutLine_StockOut", detection, StringComparison.Ordinal);
        Assert.Contains("IX_StockLedger_SourceDocument", detection, StringComparison.Ordinal);
        Assert.DoesNotContain("@FingerprintGroups", detection, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_schema_without_identity_table_is_classified_without_static_table_reference()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var start = source.IndexOf("ClassifyDatabaseAsync", StringComparison.Ordinal);
        var detection = source[start..source.IndexOf("private static void ValidateRelease", start, StringComparison.Ordinal)];

        Assert.Contains("DECLARE @Owned bit = 0", detection, StringComparison.Ordinal);
        Assert.Contains("EXEC sys.sp_executesql", detection, StringComparison.Ordinal);
        Assert.Contains("@Owned bit OUTPUT", detection, StringComparison.Ordinal);
    }

    [Fact]
    public void Legacy_schema_without_cutover_table_is_classified_without_static_table_reference()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var start = source.IndexOf("ClassifyDatabaseAsync", StringComparison.Ordinal);
        var detection = source[start..source.IndexOf("private static void ValidateRelease", start, StringComparison.Ordinal)];

        Assert.Contains("DECLARE @InstallerCreated bit = 0", detection, StringComparison.Ordinal);
        Assert.Contains("@InstallerCreated bit OUTPUT", detection, StringComparison.Ordinal);
    }

    [Fact]
    public void Maintenance_lock_identity_is_normalized_across_clients()
    {
        Assert.Equal(
            "WAREPRO:SCHEMAMAINTENANCE:PRODUCTMANAGEMENTDB",
            QuanLyHangHoa.Services.SchemaMaintenanceLock.SharedResource("ProductManagementDb"));
    }

    [Fact]
    public void Legacy_transfer_schema_is_repaired_before_exact_shape_validation()
    {
        var sql = DatabaseSchemaScripts.BuildUpgradeSql(6, "1.1.0");
        var runner = Read("WarePro.SetupHelper", "SetupCommands.cs");

        Assert.Contains("ALTER TABLE dbo.StockTransfer WITH CHECK ADD CONSTRAINT FK_StockTransfer_FromWarehouse", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE dbo.StockTransferLine WITH CHECK ADD CONSTRAINT FK_StockTransferLine_StockTransfer", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.AuditArchiveManifest", sql, StringComparison.Ordinal);
        Assert.Contains("TYPE_NAME", runner, StringComparison.Ordinal);
        Assert.Contains("max_length", runner, StringComparison.Ordinal);
    }
    [Fact]
    public void Prepare_arms_recovery_before_the_first_post_backup_mutation()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var prepare = source[source.IndexOf("public static async Task PrepareAsync", StringComparison.Ordinal)..
            source.IndexOf("public static async Task FinalizeAsync", StringComparison.Ordinal)];
        var backup = prepare.IndexOf("CreateAndVerifyBackupAsync", StringComparison.Ordinal);
        var journal = prepare.IndexOf("SaveCutoverStateAsync", StringComparison.Ordinal);
        var rcsi = prepare.IndexOf("READ_COMMITTED_SNAPSHOT", StringComparison.Ordinal);
        Assert.True(backup >= 0 && journal > backup && rcsi > journal);
        Assert.Contains("Preparing", prepare, StringComparison.Ordinal);
    }

    [Fact]
    public void Finalize_relocks_rechecks_sessions_and_commits_guards_before_client_floor()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var finalize = source[source.IndexOf("public static async Task FinalizeAsync", StringComparison.Ordinal)..
            source.IndexOf("public static async Task RollbackAsync", StringComparison.Ordinal)];
        var sql = DatabaseSchemaScripts.BuildFinalizeSql(6, "1.1.0");
        Assert.True(finalize.IndexOf("AcquireMaintenanceLockAsync", StringComparison.Ordinal) <
                    finalize.IndexOf("CountActiveSessionsAsync", StringComparison.Ordinal));
        Assert.Contains("BeginTransactionAsync", finalize, StringComparison.Ordinal);
        Assert.DoesNotContain("DeleteBackupBestEffortAsync", finalize, StringComparison.Ordinal);
        Assert.True(sql.LastIndexOf("EXEC sys.sp_executesql @WareProTriggerSql", StringComparison.Ordinal) <
                    sql.LastIndexOf("MinimumClientVersion = N'1.1.0'", StringComparison.Ordinal));
    }

    [Fact]
    public void Rollback_relocks_and_never_reopens_a_database_after_restore_failure()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var rollback = source[source.IndexOf("public static async Task RollbackAsync", StringComparison.Ordinal)..
            source.IndexOf("private static void ValidateRelease", StringComparison.Ordinal)];
        var restore = source[source.IndexOf("private static async Task RestoreBackupAsync", StringComparison.Ordinal)..
            source.IndexOf("private static async Task<SqlConnection> OpenConnectionAsync", StringComparison.Ordinal)];
        Assert.Contains("AcquireMaintenanceLockAsync", rollback, StringComparison.Ordinal);
        Assert.Contains("CountActiveSessionsAsync", rollback, StringComparison.Ordinal);
        Assert.DoesNotContain("finally", restore, StringComparison.Ordinal);
        Assert.True(restore.IndexOf("RESTORE DATABASE", StringComparison.Ordinal) <
                    restore.IndexOf("SET MULTI_USER", StringComparison.Ordinal));
    }

    [Fact]
    public void Installer_prepare_failure_always_invokes_database_rollback()
    {
        var script = Read("installer", "WarePro.iss");
        var prepare = script[script.IndexOf("procedure PrepareDatabaseCutover", StringComparison.Ordinal)..
            script.IndexOf("function FinalizeDatabaseCutover", StringComparison.Ordinal)];
        Assert.True(prepare.IndexOf("DatabaseCutoverStarted := True", StringComparison.Ordinal) <
                    prepare.IndexOf("RunSetupHelper(Arguments", StringComparison.Ordinal));
        Assert.Contains("RollbackDatabaseCutover", prepare, StringComparison.Ordinal);
    }
    [Fact]
    public void Database_signature_distinguishes_empty_owned_legacy_and_unrelated_targets()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        foreach (var marker in new[]
                 {
                     "Empty = 0", "WarePro = 1", "LegacyWarePro = 2", "Unrelated = 3",
                     "__WareProDatabaseIdentity", "is_ms_shipped", "ClassifyDatabaseAsync"
                 })
            Assert.Contains(marker, source, StringComparison.Ordinal);
        Assert.DoesNotContain("businessTableCount", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_restart_resume_reruns_full_prepare_and_clears_marker_only_after_finalize()
    {
        var script = Read("installer", "WarePro.iss");
        foreach (var marker in new[]
                 {
                     "PendingFullInstall", "ResumeFullMode", "ReadPendingFullInstall",
                     "WritePendingFullInstall", "ClearPendingFullInstall",
                     "PrepareDatabaseCutover", "FinalizeDatabaseCutover"
                 })
            Assert.Contains(marker, script, StringComparison.Ordinal);
        var finalize = script[script.IndexOf("procedure CurStepChanged", StringComparison.Ordinal)..
            script.IndexOf("procedure DeinitializeSetup", StringComparison.Ordinal)];
        Assert.True(finalize.IndexOf("DatabaseFinalized := True", StringComparison.Ordinal) <
                    finalize.IndexOf("ClearPendingFullInstall", StringComparison.Ordinal));
    }
    [Fact]
    public void Prepare_blocks_legacy_connections_before_backup_and_preserves_recovery_state()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var prepare = source[source.IndexOf("public static async Task PrepareAsync", StringComparison.Ordinal)..
            source.IndexOf("public static async Task FinalizeAsync", StringComparison.Ordinal)];

        var restrict = prepare.IndexOf("SetDatabaseAccessAsync(connectionString, restricted: true", StringComparison.Ordinal);
        var backup = prepare.IndexOf("CreateAndVerifyBackupAsync", StringComparison.Ordinal);
        var ddl = prepare.IndexOf("BuildUpgradeSql", StringComparison.Ordinal);
        Assert.True(restrict >= 0 && restrict < backup && restrict < ddl);
        Assert.Contains("installerCreatedDatabase", prepare, StringComparison.Ordinal);
        Assert.Contains("ResolveExistingCutoverAsync", prepare, StringComparison.Ordinal);
        Assert.Contains("RollbackAsync", prepare, StringComparison.Ordinal);
    }

    [Fact]
    public void Cutover_journal_keeps_original_backup_and_can_drop_only_installer_created_database()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var journal = source[source.IndexOf("private static Task SaveCutoverStateAsync", StringComparison.Ordinal)..
            source.IndexOf("private static async Task ValidateAsync", StringComparison.Ordinal)];
        var rollback = source[source.IndexOf("public static async Task RollbackAsync", StringComparison.Ordinal)..
            source.IndexOf("private enum DatabaseClassification", StringComparison.Ordinal)];

        Assert.Contains("InstallerCreatedDatabase bit NOT NULL", journal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN COALESCE(target.BackupPath, @backupPath) ELSE @backupPath END", journal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InstallerCreatedDatabase FROM dbo.__WareProUpgradeCutover", rollback, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DropDatabaseAsync", rollback, StringComparison.Ordinal);
        Assert.Contains("DROP DATABASE", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_fingerprint_requires_distinctive_relational_shape()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var start = source.IndexOf("ClassifyDatabaseAsync", StringComparison.Ordinal);
        var detection = source[start..source.IndexOf("private static void ValidateRelease", start, StringComparison.Ordinal)];

        foreach (var marker in new[]
                 {
                     "FK_StockInLine_StockIn", "FK_StockOutLine_StockOut",
                     "IX_StockLedger_Warehouse_Product_PostedAt", "DocumentCode", "BaseQuantity",
                     "__WareProUpgradeCutover"
                 })
            Assert.Contains(marker, detection, StringComparison.Ordinal);
        Assert.DoesNotContain("@FingerprintGroups >= 2", detection, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_app_has_no_public_database_creation_or_migration_entrypoint()
    {
        var initializerPath = Path.Combine(Root, "QuanLyHangHoa", "Services", "DatabaseInitializer.cs");
        var startup = Read("QuanLyHangHoa", "Startup", "StartupCoordinator.cs");

        Assert.False(File.Exists(initializerPath));
        Assert.DoesNotContain("EnsureCreated", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildUpgradeSql", startup, StringComparison.Ordinal);
        Assert.DoesNotContain("DatabaseSeeder", startup, StringComparison.Ordinal);
    }

    [Fact]
    public void Silent_smoke_provisions_secrets_before_install_without_command_line_values()
    {
        var smoke = Read("installer", "tests", "Invoke-WareProInstallerSmoke.ps1");
        var installer = Read("installer", "WarePro.iss");

        Assert.Contains("$BootstrapAdminCredential", smoke, StringComparison.Ordinal);
        Assert.Contains("WAREPROBOOTSTRAPSECRETFILE", smoke, StringComparison.Ordinal);
        Assert.Contains("Save-WareProSqlCredential", smoke, StringComparison.Ordinal);
        var appOnly = smoke[smoke.IndexOf("'AppOnly' {", StringComparison.Ordinal)..
            smoke.IndexOf("'Upgrade' {", StringComparison.Ordinal)];
        Assert.True(appOnly.IndexOf("Save-WareProSqlCredential", StringComparison.Ordinal) <
                    appOnly.IndexOf("Invoke-ProcessAndCheckExitCode", StringComparison.Ordinal));
        Assert.Contains("ExpandConstant('{param:WAREPROBOOTSTRAPSECRETFILE|}')", installer, StringComparison.Ordinal);
        Assert.DoesNotContain("WAREPROBOOTSTRAPPASSWORD", smoke, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Cutover_journal_reuses_recovery_only_for_same_active_release()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var journal = source[source.IndexOf("private static Task SaveCutoverStateAsync", StringComparison.Ordinal)..
            source.IndexOf("private static async Task ValidateAsync", StringComparison.Ordinal)];

        Assert.Contains("target.Status IN (N'Preparing', N'Prepared')", journal, StringComparison.Ordinal);
        Assert.Contains("target.PreparedByVersion = @version", journal, StringComparison.Ordinal);
        Assert.Contains("target.ExpectedSchema = @schema", journal, StringComparison.Ordinal);
        Assert.Contains("THEN COALESCE(target.BackupPath, @backupPath) ELSE @backupPath END", journal, StringComparison.Ordinal);
        Assert.Contains("THEN target.InstallerCreatedDatabase | @installerCreated ELSE @installerCreated END", journal, StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_existing_database_uses_backup_and_missing_database_rollback_is_idempotent()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var prepare = source[source.IndexOf("public static async Task PrepareAsync", StringComparison.Ordinal)..
            source.IndexOf("public static async Task FinalizeAsync", StringComparison.Ordinal)];
        var rollback = source[source.IndexOf("public static async Task RollbackAsync", StringComparison.Ordinal)..
            source.IndexOf("private enum DatabaseClassification", StringComparison.Ordinal)];

        Assert.Contains("if (!installerCreatedDatabase)", prepare, StringComparison.Ordinal);
        Assert.Contains("backupPath = await CreateAndVerifyBackupAsync", prepare, StringComparison.Ordinal);
        Assert.DoesNotContain("hasBusinessTables", prepare, StringComparison.Ordinal);
        Assert.Contains("if (target is null)", rollback, StringComparison.Ordinal);
        Assert.Contains("return;", rollback, StringComparison.Ordinal);
    }

    [Fact]
    public void Rollback_treats_only_missing_database_error_as_idempotent_success()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");
        var rollback = source[source.IndexOf("public static async Task RollbackAsync", StringComparison.Ordinal)..
            source.IndexOf("private enum DatabaseClassification", StringComparison.Ordinal)];

        Assert.Contains("catch (SqlException ex) when (ex.Number == 4060)", rollback, StringComparison.Ordinal);
        Assert.DoesNotContain("catch (SqlException)\n", rollback, StringComparison.Ordinal);
        var missingDatabaseCatch = rollback.IndexOf("catch (SqlException ex) when (ex.Number == 4060)", StringComparison.Ordinal);
        Assert.Contains("return;", rollback[missingDatabaseCatch..], StringComparison.Ordinal);
    }
    [Fact]
    public void Maintenance_commands_have_configurable_non_default_timeouts()
    {
        var source = Read("WarePro.SetupHelper", "SetupCommands.cs");

        foreach (var marker in new[]
                 {
                     "WAREPRO_SQL_CATALOG_TIMEOUT_SECONDS", "WAREPRO_SQL_MIGRATION_TIMEOUT_SECONDS",
                     "WAREPRO_SQL_BACKUP_TIMEOUT_SECONDS", "WAREPRO_SQL_VERIFY_TIMEOUT_SECONDS",
                     "WAREPRO_SQL_RESTORE_TIMEOUT_SECONDS", "CommandTimeout = MaintenanceCommandTimeouts."
                 })
            Assert.Contains(marker, source, StringComparison.Ordinal);
        foreach (var value in new[] { "DefaultCatalogSeconds = 60", "DefaultMigrationSeconds = 300", "DefaultBackupSeconds = 600", "DefaultVerifySeconds = 300", "DefaultRestoreSeconds = 600" })
            Assert.Contains(value, source, StringComparison.Ordinal);
    }
    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([Root, .. parts]));
}
