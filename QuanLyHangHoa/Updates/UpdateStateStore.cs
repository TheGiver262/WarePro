using System;
using System.IO;
using System.Text;
using System.Text.Json;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Updates;

/// <summary>
/// trạng thái cục bộ chỉ dùng để giới hạn tần suất kiểm tra tự động.
/// </summary>
public sealed class UpdateState
{
    public DateTimeOffset? LastAutomaticCheckUtc { get; set; }
}

/// <summary>
/// tách cách lưu state khỏi UpdateService để kiểm thử thời gian và nhánh skip.
/// </summary>
public interface IUpdateStateStore
{
    UpdateState Load();
    void Save(UpdateState state);
}

/// <summary>
/// lưu state theo người dùng bằng file JSON thay thế nguyên tử.
/// </summary>
public sealed class UpdateStateStore : IUpdateStateStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public UpdateStateStore(string? path = null)
    {
        _path = Path.GetFullPath(path ?? WareProPaths.Current.UpdateStatePath);
    }

    public UpdateState Load()
    {
        if (!File.Exists(_path))
        {
            return new UpdateState();
        }

        try
        {
            return JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(_path), _jsonOptions)
                ?? new UpdateState();
        }
        // state hỏng không phải dữ liệu nghiệp vụ; quay về state mới để lần kiểm tra sau vẫn hoạt động.
        catch (JsonException)
        {
            return new UpdateState();
        }
    }

    public void Save(UpdateState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        // ghi file cùng thư mục rồi replace để không để JSON chính ở trạng thái viết dở.
        var temporaryPath = _path + ".tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(state, _jsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            // file hiện có dùng replace nguyên tử; lần đầu dùng move sau khi ghi xong file tạm.
            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        // dọn file state tạm nhưng không nuốt lỗi ghi ban đầu.
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
