using System.Text.Json;
using System.Text.Json.Serialization;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;

namespace Clippings.Platform.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();
    private readonly string _path;

    public JsonSettingsStore()
        : this(PathUtil.DefaultDataRoot)
    {
    }

    public JsonSettingsStore(string dataRoot)
    {
        _path = PathUtil.SettingsPath(dataRoot);
    }

    public string SettingsPath => _path;

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
                settings.Annotation ??= new AnnotationSettings();
                settings.Recording ??= new RecordingSettings();
                return settings;
            }
            catch (JsonException)
            {
                return new AppSettings();
            }
            catch (IOException)
            {
                return new AppSettings();
            }
            catch (UnauthorizedAccessException)
            {
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, Options));
            File.Move(tempPath, _path, overwrite: true);
        }
    }
}
