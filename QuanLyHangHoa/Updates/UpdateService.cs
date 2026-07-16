using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Updates;

/// <summary>
/// lỗi xác minh gói cập nhật có mã riêng để giao diện báo đúng nguyên nhân từ chối.
/// </summary>
public sealed class UpdateSecurityException : Exception
{
    public UpdateSecurityException(string code, string message)
        : base($"{code}: {message}")
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// ranh giới mở tiến trình bộ cài để logic cập nhật có thể kiểm thử mà không chạy file thật.
/// </summary>
public interface IInstallerLauncher
{
    void Launch(string fileName, string arguments);
}

public sealed class ProcessInstallerLauncher : IInstallerLauncher
{
    public void Launch(string fileName, string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        });
    }
}

/// <summary>
/// kiểm tra release, xác minh installer theo nhiều lớp rồi mới cho phép mở chế độ nâng cấp.
/// </summary>
public sealed class UpdateService
{
    private static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(24);

    private readonly IReleaseClient _releaseClient;
    private readonly IUpdateStateStore _stateStore;
    private readonly IAuthenticodeVerifier _authenticodeVerifier;
    private readonly IInstallerLauncher _installerLauncher;
    private readonly string _expectedPublisherThumbprint;
    private readonly Func<DateTimeOffset> _utcNowProvider;

    public UpdateService(
        IReleaseClient releaseClient,
        IUpdateStateStore stateStore,
        IAuthenticodeVerifier authenticodeVerifier,
        IInstallerLauncher installerLauncher,
        string expectedPublisherThumbprint,
        Func<DateTimeOffset> utcNowProvider)
    {
        _releaseClient = releaseClient ?? throw new ArgumentNullException(nameof(releaseClient));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _authenticodeVerifier = authenticodeVerifier ?? throw new ArgumentNullException(nameof(authenticodeVerifier));
        _installerLauncher = installerLauncher ?? throw new ArgumentNullException(nameof(installerLauncher));
        _expectedPublisherThumbprint = NormalizeThumbprint(expectedPublisherThumbprint);
        _utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
    }

    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        int currentSchemaVersion,
        bool manual,
        CancellationToken cancellationToken)
    {
        // lấy thời gian qua provider để nhánh giới hạn tần suất có thể kiểm thử ổn định.
        var now = _utcNowProvider().ToUniversalTime();
        if (!manual)
        {
            var state = _stateStore.Load();
            if (state.LastAutomaticCheckUtc is { } last
                && now - last.ToUniversalTime() < AutomaticCheckInterval)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Skipped);
            }

            // ghi mốc trước khi gọi mạng để máy offline không lặp request ở mỗi lần mở màn hình.
            state.LastAutomaticCheckUtc = now;
            _stateStore.Save(state);
        }

        // timeout do HttpClient nhưng token của caller chưa hủy được coi là trạng thái offline, không phải lỗi ứng dụng.
        UpdateRelease? release;
        try
        {
            release = await _releaseClient.GetLatestAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Offline, ErrorCode: "UPD-OFFLINE");
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Offline, ErrorCode: "UPD-OFFLINE");
        }

        if (release is null)
        {
            return new UpdateCheckResult(UpdateCheckStatus.InvalidRelease, ErrorCode: "UPD-RELEASE-INVALID");
        }

        try
        {
            // parse toàn bộ phiên bản trước, sau đó đối chiếu metadata GitHub, manifest và schema đang cài.
            var manifest = release.Manifest;
            var manifestVersion = SemanticVersion.Parse(manifest.Version);
            var minimumClientVersion = SemanticVersion.Parse(manifest.MinimumClientVersion);
            var installedVersion = SemanticVersion.Parse(currentVersion);

            // chỉ chấp nhận release chính thức, asset đúng tên/size và phạm vi schema chứa database hiện tại.
            if (release.Draft
                || release.Prerelease
                || manifest.SchemaVersion != 1
                || manifestVersion.CompareTo(release.Version) != 0
                || !string.Equals(
                    manifest.InstallerAssetName,
                    GitHubReleaseClient.InstallerAssetName,
                    StringComparison.Ordinal)
                || manifest.InstallerSize != release.InstallerSize
                || manifest.MinimumSchemaVersion > manifest.MaximumSchemaVersion
                || currentSchemaVersion < manifest.MinimumSchemaVersion
                || currentSchemaVersion > manifest.MaximumSchemaVersion)
            {
                return new UpdateCheckResult(UpdateCheckStatus.InvalidRelease, ErrorCode: "UPD-RELEASE-INVALID");
            }

            // không hạ phiên bản và không cài lại cùng phiên bản qua luồng update.
            if (release.Version.CompareTo(installedVersion) <= 0)
            {
                return new UpdateCheckResult(UpdateCheckStatus.NoUpdate);
            }

            // bản cập nhật bắt buộc nếu manifest yêu cầu hoặc client hiện tại thấp hơn mức tối thiểu hỗ trợ.
            var mandatory = manifest.Mandatory || installedVersion.CompareTo(minimumClientVersion) < 0;
            return new UpdateCheckResult(
                UpdateCheckStatus.UpdateAvailable,
                new UpdateCandidate(release, mandatory));
        }
        catch (FormatException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.InvalidRelease, ErrorCode: "UPD-RELEASE-INVALID");
        }
    }
    // file tải về mang đuôi .partial và chỉ đổi tên sau khi qua đủ kiểm tra.
    // vì vậy một lần tải dở không bao giờ được coi là bộ cài hợp lệ.

    public async Task<PreparedUpdate> DownloadAndPrepareAsync(
        UpdateCandidate candidate,
        string downloadDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Directory.CreateDirectory(downloadDirectory);
        // tên file theo phiên bản giúp cache nhiều bản; đuôi .partial phân biệt file chưa được xác minh.
        var version = candidate.Version.ToString();
        var finalPath = Path.Combine(downloadDirectory, $"WarePro-Setup-{version}.exe");
        // lần tải mới xóa file dở cũ; mọi nhánh lỗi phía dưới cũng dọn đúng đường dẫn này.
        var partialPath = finalPath + ".partial";

        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        try
        {
            await _releaseClient.DownloadInstallerAsync(
                candidate.Release,
                partialPath,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // so cả API GitHub và manifest để phát hiện asset bị thay hoặc manifest bị lệch.

            // size phải khớp cả API GitHub lẫn manifest trước khi đọc toàn bộ file để băm.
            var actualSize = new FileInfo(partialPath).Length;
            if (actualSize != candidate.Release.InstallerSize
                || actualSize != candidate.Release.Manifest.InstallerSize)
            {
                throw new UpdateSecurityException("UPD-SIZE-MISMATCH", "Downloaded installer size is invalid.");
            }

            await using (var stream = File.OpenRead(partialPath))
            {
                // hash bảo vệ nội dung byte; chữ ký bên dưới bảo vệ danh tính nhà phát hành.
                var actualHash = Convert.ToHexString(
                    await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
                if (!string.Equals(
                    actualHash,
                    candidate.Release.Manifest.Sha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new UpdateSecurityException("UPD-HASH-MISMATCH", "Downloaded installer hash is invalid.");
                }
            }
            // tách chữ ký, chuỗi CA và timestamp để báo đúng nguyên nhân.
            // thumbprint còn được so với publisher đã ghim trong ứng dụng.

            var signature = _authenticodeVerifier.Verify(partialPath);
            if (!signature.SignatureValid)
            {
                throw new UpdateSecurityException("UPD-SIGNATURE-INVALID", "Installer signature is invalid.");
            }

            if (!signature.ChainValid)
            {
                throw new UpdateSecurityException("UPD-CHAIN-INVALID", "Installer certificate chain is invalid.");
            }

            if (!signature.TimestampValid)
            {
                throw new UpdateSecurityException("UPD-TIMESTAMP-INVALID", "Installer timestamp is invalid.");
            }

            if (string.IsNullOrWhiteSpace(_expectedPublisherThumbprint)
                || !string.Equals(
                    NormalizeThumbprint(signature.Thumbprint),
                    _expectedPublisherThumbprint,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateSecurityException("UPD-PUBLISHER-MISMATCH", "Installer publisher is not trusted.");
            }

            // chỉ đổi sang tên .exe cuối cùng sau khi mọi lớp kiểm tra bảo mật đều thành công.
            File.Move(partialPath, finalPath, overwrite: true);
            return new PreparedUpdate(version, finalPath);
        }
        // mọi lỗi tải hoặc xác minh đều xóa file .partial để nó không thể được dùng lại như gói hợp lệ.
        catch
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }

            throw;
        }
    }

    public void LaunchPreparedInstaller(PreparedUpdate update, string logPath)
    {
        ArgumentNullException.ThrowIfNull(update);
        // app-only giữ nguyên SQL Server; upgrade cho phép bộ cài thay file ứng dụng và ghi log riêng.
        var arguments =
            $"/CLOSEAPPLICATIONS /NORESTART /WAREPROMODE=upgrade /TYPE=app-only /LOG=\"{Path.GetFullPath(logPath)}\"";
        _installerLauncher.Launch(Path.GetFullPath(update.InstallerPath), arguments);
    }

    // bỏ khoảng trắng để thumbprint từ certificate và giá trị ghim so sánh theo cùng định dạng.
    private static string NormalizeThumbprint(string? value) =>
        (value ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
}
