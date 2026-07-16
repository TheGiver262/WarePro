using System.Threading;
using System.Threading.Tasks;

namespace QuanLyHangHoa.Updates;

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
