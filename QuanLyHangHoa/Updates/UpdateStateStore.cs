using System;
using System.IO;
using System.Text;
using System.Text.Json;
using QuanLyHangHoa.Configuration;

namespace QuanLyHangHoa.Updates;

public sealed class UpdateState
{
    public DateTimeOffset? LastAutomaticCheckUtc { get; set; }
}

public interface IUpdateStateStore
{
    UpdateState Load();
    void Save(UpdateState state);
}

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
        catch (JsonException)
        {
            return new UpdateState();
        }
    }

    public void Save(UpdateState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(state, _jsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
