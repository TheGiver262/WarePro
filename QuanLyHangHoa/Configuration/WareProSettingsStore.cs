using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuanLyHangHoa.Configuration;

/// <summary>
/// bọc lỗi JSON và lỗi phiên bản bằng một mã cấu hình ổn định cho luồng startup.
/// </summary>
public sealed class WareProConfigurationException : Exception
{
    public WareProConfigurationException(string configurationPath, Exception? innerException = null)
        : base($"CFG-CONFIG-INVALID: WarePro configuration is invalid: {configurationPath}", innerException)
    {
        Code = "CFG-CONFIG-INVALID";
        ConfigurationPath = Path.GetFullPath(configurationPath);
    }

    public string Code { get; }
    public string ConfigurationPath { get; }
}

/// <summary>
/// đọc, kiểm tra và ghi file cấu hình máy mà không để lại file chính ở trạng thái dở dang.
/// </summary>
public sealed class WareProSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _configurationPath;

    public WareProSettingsStore()
        : this(WareProPaths.Current.MachineConfigPath)
    {
    }

    public WareProSettingsStore(string configurationPath)
    {
        if (string.IsNullOrWhiteSpace(configurationPath))
        {
            throw new ArgumentException("Configuration path cannot be empty.", nameof(configurationPath));
        }

        _configurationPath = Path.GetFullPath(configurationPath);
    }

    /// <summary>
    /// trả null khi bộ cài chưa tạo cấu hình; nội dung đã tồn tại thì phải hợp lệ hoàn toàn.
    /// </summary>
    public WareProSettings? Load()
    {
        if (!File.Exists(_configurationPath))
        {
            return null;
        }

        try
        {
            return DeserializeAndValidate(File.ReadAllText(_configurationPath), _configurationPath);
        }
        catch (WareProConfigurationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new WareProConfigurationException(_configurationPath, ex);
        }
    }

    public void Save(WareProSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings, _configurationPath);

        var directory = Path.GetDirectoryName(_configurationPath)!;
        Directory.CreateDirectory(directory);
        // luôn ghi sang file cùng thư mục để bước replace không đi qua ổ đĩa hoặc volume khác.
        var temporaryPath = _configurationPath + ".tmp";

        try
        {
            // WriteThrough và flushToDisk hoàn tất dữ liệu file tạm trước khi thay file cấu hình đang dùng.
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
            {
                writer.Write(Serialize(settings));
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            // replace giữ thao tác thay file hiện có nguyên tử; lần ghi đầu dùng move sau khi file tạm đã flush.
            if (File.Exists(_configurationPath))
            {
                File.Replace(temporaryPath, _configurationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _configurationPath);
            }
        }
        // file tạm được dọn ở mọi nhánh nhưng lỗi ghi hoặc replace ban đầu vẫn được giữ nguyên.
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static string Serialize(WareProSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings, JsonOptions);
    }

    public static WareProSettings Deserialize(string json) =>
        DeserializeAndValidate(json, "<memory>");

    // deserialize và validate đi cùng nhau để caller không bao giờ nhận một cấu hình chỉ hợp lệ một phần.
    private static WareProSettings DeserializeAndValidate(string json, string configurationPath)
    {
        try
        {
            var settings = JsonSerializer.Deserialize<WareProSettings>(json, JsonOptions)
                ?? throw new JsonException("WarePro settings cannot be empty.");
            Validate(settings, configurationPath);
            return settings;
        }
        catch (WareProConfigurationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new WareProConfigurationException(configurationPath, ex);
        }
    }

    // kiểm tra tập trung giúp mọi entry point dùng cùng điều kiện về schema và trường bắt buộc.
    private static void Validate(WareProSettings settings, string configurationPath)
    {
        if (settings.SchemaVersion != WareProSettings.CurrentSchemaVersion
            || settings.Database is null
            || settings.Updates is null
            || string.IsNullOrWhiteSpace(settings.Database.Server)
            || string.IsNullOrWhiteSpace(settings.Database.Database)
            || string.IsNullOrWhiteSpace(settings.Updates.Repository)
            || string.IsNullOrWhiteSpace(settings.Updates.Channel)
            || settings.Updates.CheckIntervalHours <= 0)
        {
            throw new WareProConfigurationException(configurationPath);
        }
    }
}
