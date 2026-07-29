using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Startup;

namespace QuanLyHangHoa.Tests.Startup;

public sealed class FirstInstallDemoSeederTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void Default_runtime_uses_the_verified_connection_and_checks_for_the_cutover_table()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "QuanLyHangHoa", "Startup", "FirstInstallDemoSeeder.cs"));

        Assert.Contains("WareProSettings? settings", source, StringComparison.Ordinal);
        Assert.Contains("InitialDataProfile.Demo", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentRole.Server", source, StringComparison.Ordinal);
        Assert.Contains("DeploymentRole.Standalone", source, StringComparison.Ordinal);
        Assert.Contains("AppDbContextOptionsFactory.Create(_connectionString)", source, StringComparison.Ordinal);
        Assert.Contains("OBJECT_ID(N'dbo.__WareProUpgradeCutover', N'U')", source, StringComparison.Ordinal);
        Assert.Contains("sp_getapplock", source, StringComparison.Ordinal);
        Assert.Contains("@LockMode = N'Exclusive'", source, StringComparison.Ordinal);
        var lockIndex = source.IndexOf(
            "await using var lockConnection = await AcquireSeedLockAsync", StringComparison.Ordinal);
        var recheckIndex = source.IndexOf(
            "if (!await IsSeedRequiredAsync(cancellationToken))", StringComparison.Ordinal);
        Assert.True(recheckIndex > lockIndex, "Seed condition is not rechecked while the exclusive lock is held.");
    }

    [Fact]
    public async Task RunAsync_seeds_a_finalized_installer_created_empty_database()
    {
        var workbookPath = Path.GetTempFileName();
        try
        {
            var runtime = new FakeRuntime(workbookPath, shouldSeed: true);

            await new FirstInstallDemoSeeder(runtime, DemoSettings()).RunAsync(CancellationToken.None);

            Assert.Equal(1, runtime.SeedCalls);
        }
        finally
        {
            File.Delete(workbookPath);
        }
    }

    [Fact]
    public async Task RunAsync_skips_without_an_explicit_demo_profile_before_accessing_database()
    {
        var runtime = new FakeRuntime(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx"), shouldSeed: true);

        await new FirstInstallDemoSeeder(runtime).RunAsync(CancellationToken.None);

        Assert.Equal(0, runtime.SeedRequirementCalls);
        Assert.Equal(0, runtime.SeedCalls);
    }

    [Fact]
    public async Task RunAsync_skips_a_client_profile_even_if_demo_was_requested()
    {
        var runtime = new FakeRuntime(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx"), shouldSeed: true);

        await new FirstInstallDemoSeeder(runtime, DemoSettings(DeploymentRole.Client)).RunAsync(CancellationToken.None);

        Assert.Equal(0, runtime.SeedRequirementCalls);
        Assert.Equal(0, runtime.SeedCalls);
    }
    [Fact]
    public async Task RunAsync_skips_an_existing_or_nonempty_database()
    {
        var runtime = new FakeRuntime(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx"), shouldSeed: false);

        await new FirstInstallDemoSeeder(runtime, DemoSettings()).RunAsync(CancellationToken.None);

        Assert.Equal(0, runtime.SeedCalls);
    }

    [Fact]
    public async Task RunAsync_reports_the_existing_missing_workbook_error_before_writing()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
        var runtime = new FakeRuntime(workbookPath, shouldSeed: true);

        var error = await Assert.ThrowsAsync<SeedWorkbookMissingException>(
            () => new FirstInstallDemoSeeder(runtime, DemoSettings()).RunAsync(CancellationToken.None));

        Assert.Equal("DB-SEED-MISSING", error.Code);
        Assert.Equal(0, runtime.SeedCalls);
    }

    private static WareProSettings DemoSettings(DeploymentRole role = DeploymentRole.Server) =>
        new()
        {
            DeploymentRole = role,
            InitialDataProfile = InitialDataProfile.Demo
        };

    private sealed class FakeRuntime(string workbookPath, bool shouldSeed) : IFirstInstallDemoSeedRuntime
    {
        public int SeedRequirementCalls { get; private set; }
        public int SeedCalls { get; private set; }
        public string SeedWorkbookPath => workbookPath;

        public Task<bool> IsSeedRequiredAsync(CancellationToken cancellationToken)
        {
            SeedRequirementCalls++;
            return Task.FromResult(shouldSeed);
        }

        public Task SeedAsync(CancellationToken cancellationToken)
        {
            SeedCalls++;
            return Task.CompletedTask;
        }
    }
}
