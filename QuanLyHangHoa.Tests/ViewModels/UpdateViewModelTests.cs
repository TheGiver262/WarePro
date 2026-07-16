using QuanLyHangHoa.Updates;
using QuanLyHangHoa.ViewModels;

namespace QuanLyHangHoa.Tests.ViewModels;

public class UpdateViewModelTests
{
    [Fact]
    public void Initial_state_is_idle_and_download_is_disabled()
    {
        var viewModel = CreateViewModel(new FakeUpdateOperations());

        Assert.Equal(UpdateUiState.Idle, viewModel.State);
        Assert.False(viewModel.DownloadAndInstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task Check_moves_to_available_and_exposes_release_information()
    {
        var candidate = CreateCandidate("1.1.0");
        var operations = new FakeUpdateOperations
        {
            CheckResult = new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, candidate)
        };
        var viewModel = CreateViewModel(operations);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(UpdateUiState.Available, viewModel.State);
        Assert.Equal("1.1.0", viewModel.AvailableVersion);
        Assert.Equal("Sửa lỗi và cải thiện ổn định.", viewModel.ReleaseNotes);
        Assert.True(viewModel.HasUpdate);
        Assert.True(viewModel.DownloadAndInstallCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(UpdateCheckStatus.NoUpdate, UpdateUiState.UpToDate)]
    [InlineData(UpdateCheckStatus.Offline, UpdateUiState.Failed)]
    [InlineData(UpdateCheckStatus.InvalidRelease, UpdateUiState.Failed)]
    public async Task Check_maps_non_available_results_to_safe_UI_states(
        UpdateCheckStatus checkStatus,
        UpdateUiState expectedState)
    {
        var operations = new FakeUpdateOperations
        {
            CheckResult = new UpdateCheckResult(checkStatus, ErrorCode: "UPD-OFFLINE")
        };
        var viewModel = CreateViewModel(operations);

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal(expectedState, viewModel.State);
        Assert.False(viewModel.HasUpdate);
        Assert.False(viewModel.DownloadAndInstallCommand.CanExecute(null));
    }

    [Fact]
    public async Task Automatic_check_uses_the_throttled_service_mode()
    {
        var operations = new FakeUpdateOperations();
        var viewModel = CreateViewModel(operations);

        await viewModel.CheckAutomaticallyAsync();

        Assert.False(operations.LastManual);
        Assert.Equal(1, operations.CheckCount);
    }

    [Fact]
    public async Task Double_check_does_not_start_two_concurrent_requests()
    {
        var gate = new TaskCompletionSource<UpdateCheckResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var operations = new FakeUpdateOperations { CheckGate = gate };
        var viewModel = CreateViewModel(operations);

        var first = viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        var second = viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
        gate.SetResult(new UpdateCheckResult(UpdateCheckStatus.NoUpdate));
        await Task.WhenAll(first, second);

        Assert.Equal(1, operations.CheckCount);
        Assert.Equal(UpdateUiState.UpToDate, viewModel.State);
    }

    [Fact]
    public async Task Download_prepares_and_launches_only_after_the_user_command()
    {
        var candidate = CreateCandidate("1.1.0");
        var operations = new FakeUpdateOperations
        {
            CheckResult = new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, candidate)
        };
        var shutdownCount = 0;
        var viewModel = CreateViewModel(operations, () => shutdownCount++);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        await viewModel.DownloadAndInstallCommand.ExecuteAsync(null);

        Assert.Equal(UpdateUiState.Ready, viewModel.State);
        Assert.Equal(100, viewModel.ProgressPercent);
        Assert.Equal(1, operations.DownloadCount);
        Assert.Equal(1, operations.LaunchCount);
        Assert.Equal(1, shutdownCount);
    }

    [Fact]
    public async Task Failed_installer_launch_keeps_the_application_open()
    {
        var candidate = CreateCandidate("1.1.0");
        var operations = new FakeUpdateOperations
        {
            CheckResult = new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, candidate),
            LaunchException = new InvalidOperationException("launcher failed")
        };
        var shutdownCount = 0;
        var viewModel = CreateViewModel(operations, () => shutdownCount++);
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        await viewModel.DownloadAndInstallCommand.ExecuteAsync(null);

        Assert.Equal(UpdateUiState.Failed, viewModel.State);
        Assert.Equal(1, operations.LaunchCount);
        Assert.Equal(0, shutdownCount);
    }

    [Fact]
    public void Update_view_uses_safe_text_bindings_and_no_purple_or_violet()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var xaml = File.ReadAllText(Path.Combine(
            root, "QuanLyHangHoa", "Views", "UpdateView.xaml"));

        foreach (var binding in new[]
                 {
                     "CurrentVersion",
                     "AvailableVersion",
                     "ReleaseNotes",
                     "ProgressPercent",
                     "CheckForUpdatesCommand",
                     "DownloadAndInstallCommand"
                 })
        {
            Assert.Contains(binding, xaml, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("purple", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("violet", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WebBrowser", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static UpdateViewModel CreateViewModel(
        IUpdateOperations operations,
        Action? shutdownApplication = null) => new(
        operations,
        currentVersion: "1.0.0",
        currentSchemaVersion: 5,
        downloadDirectory: Path.Combine(Path.GetTempPath(), "WarePro", "Updates"),
        installerLogPath: Path.Combine(Path.GetTempPath(), "WarePro", "Logs", "update.log"),
        shutdownApplication: shutdownApplication);

    private static UpdateCandidate CreateCandidate(string version)
    {
        var manifest = new UpdateManifest
        {
            SchemaVersion = 1,
            Version = version,
            MinimumClientVersion = "1.0.0",
            MinimumSchemaVersion = 5,
            MaximumSchemaVersion = 5,
            InstallerAssetName = "WarePro-Setup.exe",
            InstallerSize = 1024,
            Sha256 = new string('a', 64),
            Mandatory = false,
            ReleaseNotes = "Sửa lỗi và cải thiện ổn định."
        };
        var release = new UpdateRelease(
            SemanticVersion.Parse(version),
            false,
            false,
            new Uri("https://example.test/WarePro-Setup.exe"),
            manifest.InstallerSize,
            manifest);
        return new UpdateCandidate(release, Mandatory: false);
    }

    private sealed class FakeUpdateOperations : IUpdateOperations
    {
        public UpdateCheckResult CheckResult { get; init; } =
            new(UpdateCheckStatus.NoUpdate);
        public TaskCompletionSource<UpdateCheckResult>? CheckGate { get; init; }
        public int CheckCount { get; private set; }
        public int DownloadCount { get; private set; }
        public int LaunchCount { get; private set; }
        public bool LastManual { get; private set; } = true;
        public Exception? LaunchException { get; init; }

        public Task<UpdateCheckResult> CheckAsync(
            string currentVersion,
            int currentSchemaVersion,
            bool manual,
            CancellationToken cancellationToken)
        {
            CheckCount++;
            LastManual = manual;
            return CheckGate?.Task ?? Task.FromResult(CheckResult);
        }

        public Task<PreparedUpdate> DownloadAndPrepareAsync(
            UpdateCandidate candidate,
            string downloadDirectory,
            CancellationToken cancellationToken)
        {
            DownloadCount++;
            return Task.FromResult(new PreparedUpdate(
                candidate.Version.ToString(),
                Path.Combine(downloadDirectory, $"WarePro-Setup-{candidate.Version}.exe")));
        }

        public void LaunchPreparedInstaller(PreparedUpdate update, string logPath)
        {
            LaunchCount++;
            if (LaunchException is not null) throw LaunchException;
        }
    }
}
