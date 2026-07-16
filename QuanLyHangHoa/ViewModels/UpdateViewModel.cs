using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QuanLyHangHoa.Configuration;
using QuanLyHangHoa.Services;
using QuanLyHangHoa.Updates;

namespace QuanLyHangHoa.ViewModels;

public enum UpdateUiState
{
    Idle,
    Checking,
    Available,
    Downloading,
    Ready,
    Failed,
    UpToDate
}

public partial class UpdateViewModel : ObservableObject
{
    private readonly IUpdateOperations _operations;
    private readonly int _currentSchemaVersion;
    private readonly string _downloadDirectory;
    private readonly string _installerLogPath;
    private readonly Action _shutdownApplication;
    private UpdateCandidate? _candidate;
    private bool _busy;

    [ObservableProperty] private UpdateUiState _state = UpdateUiState.Idle;
    [ObservableProperty] private string _currentVersion;
    [ObservableProperty] private string _availableVersion = "—";
    [ObservableProperty] private string _releaseNotes = "Chưa kiểm tra bản cập nhật.";
    [ObservableProperty] private string _lastCheck = "Chưa kiểm tra";
    [ObservableProperty] private string _downloadSize = "—";
    [ObservableProperty] private bool _isMandatory;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _statusMessage = "Bạn có thể tiếp tục sử dụng WarePro trong khi kiểm tra.";
    [ObservableProperty] private string _errorAction = string.Empty;

    public UpdateViewModel(
        IUpdateOperations operations,
        string currentVersion,
        int currentSchemaVersion,
        string downloadDirectory,
        string installerLogPath,
        Action? shutdownApplication = null)
    {
        _operations = operations ?? throw new ArgumentNullException(nameof(operations));
        _currentVersion = currentVersion;
        _currentSchemaVersion = currentSchemaVersion;
        _downloadDirectory = downloadDirectory;
        _installerLogPath = installerLogPath;
        _shutdownApplication = shutdownApplication ?? (() => { });
    }

    public bool HasUpdate => _candidate is not null;

    public event Action<bool>? UpdateAvailabilityChanged;

    public static UpdateViewModel CreateDefault()
    {
        var assembly = typeof(UpdateViewModel).Assembly;
        var currentVersion = (assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "1.0.0")
            .Split('+', 2)[0];
        var publisherThumbprint = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(attribute =>
                string.Equals(
                    attribute.Key,
                    "WareProPublisherThumbprint",
                    StringComparison.Ordinal))
            ?.Value
            ?? string.Empty;
        var settings = new WareProSettingsStore().Load() ?? WareProSettings.CreateDefault();
        var client = GitHubReleaseClient.CreateDefault(settings.Updates.Repository, currentVersion);
        var service = new UpdateService(
            client,
            new UpdateStateStore(),
            new AuthenticodeVerifier(),
            new ProcessInstallerLauncher(),
            publisherThumbprint,
            () => DateTimeOffset.UtcNow);
        var paths = WareProPaths.Current;
        return new UpdateViewModel(
            new UpdateOperationsAdapter(service),
            currentVersion,
            DatabaseCompatibilityService.CurrentSchemaVersion,
            paths.UpdateCacheDirectory,
            System.IO.Path.Combine(paths.UserLogDirectory, "update-installer.log"),
            () => Application.Current?.Shutdown());
    }

    private bool CanCheckForUpdates() => !_busy;

    public Task CheckAutomaticallyAsync() => CheckCoreAsync(manual: false);

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdates()
    {
        await CheckCoreAsync(manual: true);
    }

    private async Task CheckCoreAsync(bool manual)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        State = UpdateUiState.Checking;
        StatusMessage = "Đang kiểm tra bản cập nhật...";
        ErrorAction = string.Empty;
        NotifyCommandState();
        try
        {
            var result = await _operations.CheckAsync(
                CurrentVersion,
                _currentSchemaVersion,
                manual,
                CancellationToken.None);
            LastCheck = DateTimeOffset.Now.ToString("g", CultureInfo.CurrentCulture);
            ApplyCheckResult(result);
        }
        catch (Exception ex)
        {
            _candidate = null;
            State = UpdateUiState.Failed;
            StatusMessage = "Không thể kiểm tra cập nhật.";
            ErrorAction = "Thử lại khi kết nối mạng ổn định.";
            ReleaseNotes = SensitiveDataRedactor.Redact(ex.Message);
        }
        finally
        {
            _busy = false;
            OnPropertyChanged(nameof(HasUpdate));
            UpdateAvailabilityChanged?.Invoke(HasUpdate);
            NotifyCommandState();
        }
    }

    private bool CanDownloadAndInstall() => !_busy && _candidate is not null;

    [RelayCommand(CanExecute = nameof(CanDownloadAndInstall))]
    private async Task DownloadAndInstall()
    {
        if (_busy || _candidate is null)
        {
            return;
        }

        _busy = true;
        State = UpdateUiState.Downloading;
        ProgressPercent = 0;
        StatusMessage = "Đang tải và kiểm tra chữ ký bộ cài...";
        ErrorAction = string.Empty;
        NotifyCommandState();
        try
        {
            var package = await _operations.DownloadAndPrepareAsync(
                _candidate,
                _downloadDirectory,
                CancellationToken.None);
            ProgressPercent = 100;
            State = UpdateUiState.Ready;
            StatusMessage = "Bộ cài đã được xác thực. WarePro sẽ mở trình cập nhật.";
            _operations.LaunchPreparedInstaller(package, _installerLogPath);
            _shutdownApplication();
        }
        catch (Exception ex)
        {
            State = UpdateUiState.Failed;
            StatusMessage = "Không thể chuẩn bị bản cập nhật.";
            ErrorAction = "Kiểm tra mạng, dung lượng ổ đĩa và chữ ký bộ cài rồi thử lại.";
            ReleaseNotes = SensitiveDataRedactor.Redact(ex.Message);
        }
        finally
        {
            _busy = false;
            NotifyCommandState();
        }
    }

    private void ApplyCheckResult(UpdateCheckResult result)
    {
        _candidate = result.Candidate;
        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable when _candidate is not null:
                State = UpdateUiState.Available;
                AvailableVersion = _candidate.Version.ToString();
                ReleaseNotes = _candidate.Release.Manifest.ReleaseNotes;
                DownloadSize = FormatSize(_candidate.Release.InstallerSize);
                IsMandatory = _candidate.Mandatory;
                StatusMessage = _candidate.Mandatory
                    ? "Bản cập nhật này là bắt buộc để tiếp tục tương thích dữ liệu."
                    : "Có bản cập nhật mới. Bạn chủ động chọn thời điểm cài.";
                break;
            case UpdateCheckStatus.NoUpdate:
                State = UpdateUiState.UpToDate;
                StatusMessage = "WarePro đang ở phiên bản mới nhất.";
                ReleaseNotes = "Không có bản cập nhật mới.";
                break;
            case UpdateCheckStatus.Skipped:
                State = UpdateUiState.Idle;
                StatusMessage = "Lần kiểm tra tự động gần nhất chưa quá 24 giờ.";
                break;
            case UpdateCheckStatus.Offline:
                State = UpdateUiState.Failed;
                StatusMessage = "Không thể kết nối máy chủ cập nhật.";
                ErrorAction = "Tiếp tục làm việc và thử lại sau.";
                break;
            default:
                State = UpdateUiState.Failed;
                StatusMessage = "Thông tin bản cập nhật không hợp lệ.";
                ErrorAction = "Không cài file này; thử kiểm tra lại sau.";
                break;
        }
    }

    private void NotifyCommandState()
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        DownloadAndInstallCommand.NotifyCanExecuteChanged();
    }

    private static string FormatSize(long bytes) =>
        $"{bytes / 1024d / 1024d:N1} MB";
}
