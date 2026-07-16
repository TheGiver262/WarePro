using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Updates;

/// <summary>
/// cung cấp metadata release và tải đúng installer mà UpdateService sẽ kiểm tra bảo mật.
/// </summary>
public interface IReleaseClient
{
    Task<UpdateRelease?> GetLatestAsync(CancellationToken cancellationToken);
    Task DownloadInstallerAsync(
        UpdateRelease release,
        string destinationPath,
        CancellationToken cancellationToken);
}

/// <summary>
/// đọc release mới nhất từ GitHub và ghép installer với manifest đi kèm cùng release.
/// </summary>
public sealed class GitHubReleaseClient : IReleaseClient
{
    public const string InstallerAssetName = "WarePro-Setup.exe";
    public const string ManifestAssetName = "warepro-update.json";

    private readonly HttpClient _httpClient;
    private readonly string _repository;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubReleaseClient(HttpClient httpClient, string repository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repository = string.IsNullOrWhiteSpace(repository)
            ? throw new ArgumentException("Repository cannot be empty.", nameof(repository))
            : repository.Trim('/');
    }

    // timeout ngắn giữ kiểm tra cập nhật nền không treo ứng dụng khi mạng có vấn đề.
    public static GitHubReleaseClient CreateDefault(string repository, string appVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2026-03-10");
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"WarePro/{appVersion}");
        return new GitHubReleaseClient(client, repository);
    }

    public async Task<UpdateRelease?> GetLatestAsync(CancellationToken cancellationToken)
    {
        // API latest chỉ chọn release đã công bố; trạng thái draft và prerelease vẫn được kiểm tra lại phía service.
        var endpoint = $"https://api.github.com/repos/{_repository}/releases/latest";
        using var response = await _httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubReleaseDto>(
            stream,
            _jsonOptions,
            cancellationToken);
        if (release is null)
        {
            return null;
        }

        // yêu cầu đúng một asset theo từng tên cố định để không chọn nhầm gói hoặc manifest.
        var installer = release.Assets.SingleOrDefault(asset =>
            string.Equals(asset.Name, InstallerAssetName, StringComparison.Ordinal));
        var manifestAsset = release.Assets.SingleOrDefault(asset =>
            string.Equals(asset.Name, ManifestAssetName, StringComparison.Ordinal));
        if (installer is null || manifestAsset is null)
        {
            return null;
        }

        // manifest được tải từ chính asset của release và là nguồn checksum, size, schema compatibility.
        using var manifestResponse = await _httpClient.GetAsync(
            manifestAsset.BrowserDownloadUrl,
            cancellationToken);
        manifestResponse.EnsureSuccessStatusCode();
        await using var manifestStream = await manifestResponse.Content.ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(
            manifestStream,
            _jsonOptions,
            cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        // giữ cả metadata GitHub và manifest để lớp bảo mật đối chiếu hai nguồn trước khi tải.
        return new UpdateRelease(
            SemanticVersion.Parse(release.TagName),
            release.Draft,
            release.Prerelease,
            new Uri(installer.BrowserDownloadUrl, UriKind.Absolute),
            installer.Size,
            manifest);
    }

    public async Task DownloadInstallerAsync(
        UpdateRelease release,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        // chỉ giữ response headers trong bộ nhớ; nội dung installer được stream thẳng xuống file đích.
        using var response = await _httpClient.GetAsync(
            release.InstallerUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        // flush hoàn tất dữ liệu đã tải trước khi UpdateService bắt đầu đo size và băm file.
        await source.CopyToAsync(destination, cancellationToken);
        await destination.FlushAsync(cancellationToken);
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAssetDto[] Assets { get; set; } = [];
    }

    private sealed class GitHubAssetDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}
