using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Updates;

/// <summary>
/// hợp đồng ba bước mà ViewModel dùng: kiểm tra, chuẩn bị gói đã xác minh và mở bộ cài.
/// </summary>
public interface IUpdateOperations
{
    Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        int currentSchemaVersion,
        bool manual,
        CancellationToken cancellationToken);

    Task<PreparedUpdate> DownloadAndPrepareAsync(
        UpdateCandidate candidate,
        string downloadDirectory,
        CancellationToken cancellationToken);

    void LaunchPreparedInstaller(PreparedUpdate update, string logPath);
}

/// <summary>
/// adapter giữ ViewModel phụ thuộc interface trong khi toàn bộ quy tắc vẫn nằm ở UpdateService.
/// </summary>
public sealed class UpdateOperationsAdapter : IUpdateOperations
{
    private readonly UpdateService _service;

    public UpdateOperationsAdapter(UpdateService service)
    {
        _service = service;
    }

    public Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        int currentSchemaVersion,
        bool manual,
        CancellationToken cancellationToken) =>
        _service.CheckAsync(currentVersion, currentSchemaVersion, manual, cancellationToken);

    public Task<PreparedUpdate> DownloadAndPrepareAsync(
        UpdateCandidate candidate,
        string downloadDirectory,
        CancellationToken cancellationToken) =>
        _service.DownloadAndPrepareAsync(candidate, downloadDirectory, cancellationToken);

    public void LaunchPreparedInstaller(PreparedUpdate update, string logPath) =>
        _service.LaunchPreparedInstaller(update, logPath);
}
