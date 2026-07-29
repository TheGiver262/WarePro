using System.Net.Http;
using System.Security.Cryptography;
using QuanLyHangHoa.Updates;

namespace QuanLyHangHoa.Tests.Updates;

public class UpdateServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "WarePro.Update.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("1.2.0", "1.1.9", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-beta.2", "1.0.0-beta.10", -1)]
    [InlineData("1.0.0", "1.0.0-rc.1", 1)]
    public void Semantic_version_comparison_handles_prerelease_identifiers(
        string left,
        string right,
        int expectedSign)
    {
        var comparison = SemanticVersion.Parse(left).CompareTo(SemanticVersion.Parse(right));

        Assert.Equal(expectedSign, Math.Sign(comparison));
    }

    [Fact]
    public async Task Check_returns_only_a_newer_stable_compatible_release()
    {
        var release = CreateRelease("1.1.0");
        var service = CreateService(new FakeReleaseClient(release));

        var result = await service.CheckAsync("1.0.0", currentSchemaVersion: 5, manual: true, CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.1.0", result.Candidate!.Version.ToString());
        Assert.False(result.Candidate.Mandatory);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Check_rejects_draft_and_prerelease_releases(bool draft, bool prerelease)
    {
        var release = CreateRelease("1.1.0") with { Draft = draft, Prerelease = prerelease };
        var service = CreateService(new FakeReleaseClient(release));

        var result = await service.CheckAsync("1.0.0", 5, manual: true, CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.InvalidRelease, result.Status);
    }

    [Fact]
    public async Task Automatic_check_runs_at_most_once_per_24_hours_but_manual_check_always_runs()
    {
        var client = new FakeReleaseClient(CreateRelease("1.1.0"));
        var state = new FakeUpdateStateStore
        {
            State = new UpdateState { LastAutomaticCheckUtc = new DateTimeOffset(2026, 7, 16, 2, 0, 0, TimeSpan.Zero) }
        };
        var service = CreateService(client, state, utcNow: new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero));

        var automatic = await service.CheckAsync("1.0.0", 5, manual: false, CancellationToken.None);
        var manual = await service.CheckAsync("1.0.0", 5, manual: true, CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Skipped, automatic.Status);
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, manual.Status);
        Assert.Equal(1, client.GetLatestCount);
    }

    [Fact]
    public async Task Offline_check_does_not_fail_the_compatible_application()
    {
        var service = CreateService(new FakeReleaseClient(new HttpRequestException("offline")));

        var result = await service.CheckAsync("1.0.0", 5, manual: false, CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Offline, result.Status);
        Assert.Null(result.Candidate);
    }

    [Fact]
    public async Task Download_renames_partial_only_after_all_integrity_gates_pass()
    {
        var bytes = "signed installer bytes"u8.ToArray();
        var release = CreateRelease("1.1.0", bytes);
        var client = new FakeReleaseClient(release, bytes);
        var service = CreateService(client);
        var candidate = (await service.CheckAsync("1.0.0", 5, true, CancellationToken.None)).Candidate!;

        var package = await service.DownloadAndPrepareAsync(candidate, _directory, CancellationToken.None);

        Assert.Equal(Path.Combine(_directory, "WarePro-Setup-1.1.0.exe"), package.InstallerPath);
        Assert.True(File.Exists(package.InstallerPath));
        Assert.False(File.Exists(package.InstallerPath + ".partial"));
    }

    [Fact]
    public async Task Download_hash_mismatch_blocks_package_and_removes_partial_file()
    {
        var bytes = "tampered"u8.ToArray();
        var release = CreateRelease("1.1.0", "expected"u8.ToArray());
        var service = CreateService(new FakeReleaseClient(release, bytes));
        var candidate = (await service.CheckAsync("1.0.0", 5, true, CancellationToken.None)).Candidate!;

        var error = await Assert.ThrowsAsync<UpdateSecurityException>(() =>
            service.DownloadAndPrepareAsync(candidate, _directory, CancellationToken.None));

        Assert.Equal("UPD-HASH-MISMATCH", error.Code);
        Assert.Empty(Directory.GetFiles(_directory, "*.partial"));
    }

    [Theory]
    [InlineData(false, true, true, "UPD-SIGNATURE-INVALID")]
    [InlineData(true, false, true, "UPD-CHAIN-INVALID")]
    [InlineData(true, true, false, "UPD-TIMESTAMP-INVALID")]
    public async Task Authenticode_gate_blocks_invalid_signature_chain_or_timestamp(
        bool signatureValid,
        bool chainValid,
        bool timestampValid,
        string expectedCode)
    {
        var bytes = "installer"u8.ToArray();
        var release = CreateRelease("1.1.0", bytes);
        var verifier = new FakeAuthenticodeVerifier(new(
            signatureValid,
            chainValid,
            timestampValid,
            "AABBCC"));
        var service = CreateService(new FakeReleaseClient(release, bytes), verifier: verifier);
        var candidate = (await service.CheckAsync("1.0.0", 5, true, CancellationToken.None)).Candidate!;

        var error = await Assert.ThrowsAsync<UpdateSecurityException>(() =>
            service.DownloadAndPrepareAsync(candidate, _directory, CancellationToken.None));

        Assert.Equal(expectedCode, error.Code);
    }

    [Fact]
    public async Task Cancellation_removes_the_partial_download()
    {
        var bytes = "installer"u8.ToArray();
        var release = CreateRelease("1.1.0", bytes);
        var client = new FakeReleaseClient(release, cancellationAfterWrite: true);
        var service = CreateService(client);
        var candidate = (await service.CheckAsync("1.0.0", 5, true, CancellationToken.None)).Candidate!;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DownloadAndPrepareAsync(candidate, _directory, new CancellationToken(canceled: false)));

        Assert.Empty(Directory.GetFiles(_directory, "*.partial"));
    }

    [Fact]
    public void Launch_arguments_contain_only_safe_installer_switches_and_log_path()
    {
        var launcher = new FakeInstallerLauncher();
        var service = CreateService(new FakeReleaseClient(CreateRelease("1.1.0")), launcher: launcher);
        var installer = Path.Combine(_directory, "WarePro-Setup-1.1.0.exe");
        var log = Path.Combine(_directory, "update.log");

        service.LaunchPreparedInstaller(new PreparedUpdate("1.1.0", installer), log);

        Assert.Equal(installer, launcher.FileName);
        Assert.Contains("/CLOSEAPPLICATIONS", launcher.Arguments, StringComparison.Ordinal);
        Assert.Contains("/NORESTART", launcher.Arguments, StringComparison.Ordinal);
        Assert.Contains("/WAREPROMODE=upgrade", launcher.Arguments, StringComparison.Ordinal);
        Assert.Contains("/TYPE=client", launcher.Arguments, StringComparison.Ordinal);
        Assert.Contains(log, launcher.Arguments, StringComparison.Ordinal);
        Assert.DoesNotContain("password", launcher.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", launcher.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", launcher.Arguments, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static UpdateRelease CreateRelease(string version, byte[]? bytes = null)
    {
        bytes ??= "installer"u8.ToArray();
        return new UpdateRelease(
            SemanticVersion.Parse(version),
            Draft: false,
            Prerelease: false,
            new Uri("https://example.test/WarePro-Setup.exe"),
            bytes.LongLength,
            new UpdateManifest
            {
                SchemaVersion = 1,
                Version = version,
                MinimumClientVersion = "1.0.0",
                MinimumSchemaVersion = 5,
                MaximumSchemaVersion = 5,
                InstallerAssetName = "WarePro-Setup.exe",
                InstallerSize = bytes.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                Mandatory = false
            });
    }

    private static UpdateService CreateService(
        IReleaseClient client,
        IUpdateStateStore? state = null,
        IAuthenticodeVerifier? verifier = null,
        IInstallerLauncher? launcher = null,
        DateTimeOffset? utcNow = null) =>
        new(
            client,
            state ?? new FakeUpdateStateStore(),
            verifier ?? new FakeAuthenticodeVerifier(new(true, true, true, "AABBCC")),
            launcher ?? new FakeInstallerLauncher(),
            expectedPublisherThumbprint: "AABBCC",
            () => utcNow ?? new DateTimeOffset(2026, 7, 16, 3, 0, 0, TimeSpan.Zero));

    private sealed class FakeReleaseClient : IReleaseClient
    {
        private readonly UpdateRelease? _release;
        private readonly Exception? _failure;
        private readonly byte[] _download;
        private readonly bool _cancellationAfterWrite;

        public FakeReleaseClient(UpdateRelease release, byte[]? download = null, bool cancellationAfterWrite = false)
        {
            _release = release;
            _download = download ?? "installer"u8.ToArray();
            _cancellationAfterWrite = cancellationAfterWrite;
        }

        public FakeReleaseClient(Exception failure)
        {
            _failure = failure;
            _download = [];
        }

        public int GetLatestCount { get; private set; }

        public Task<UpdateRelease?> GetLatestAsync(CancellationToken cancellationToken)
        {
            GetLatestCount++;
            return _failure is null
                ? Task.FromResult(_release)
                : Task.FromException<UpdateRelease?>(_failure);
        }

        public async Task DownloadInstallerAsync(
            UpdateRelease release,
            string destinationPath,
            CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await File.WriteAllBytesAsync(destinationPath, _download, cancellationToken);
            if (_cancellationAfterWrite)
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
    }

    private sealed class FakeUpdateStateStore : IUpdateStateStore
    {
        public UpdateState State { get; set; } = new();
        public UpdateState Load() => State;
        public void Save(UpdateState state) => State = state;
    }

    private sealed class FakeAuthenticodeVerifier(AuthenticodeVerificationResult result) : IAuthenticodeVerifier
    {
        public AuthenticodeVerificationResult Verify(string filePath) => result;
    }

    private sealed class FakeInstallerLauncher : IInstallerLauncher
    {
        public string FileName { get; private set; } = string.Empty;
        public string Arguments { get; private set; } = string.Empty;

        public void Launch(string fileName, string arguments)
        {
            FileName = fileName;
            Arguments = arguments;
        }
    }
}
