using System.Text.Json;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;

namespace Clippings.Platform.Storage;

public sealed class JsonSettingsStore : ISettingsStore
{
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
                using var stream = File.OpenRead(_path);
                var settings = JsonSerializer.Deserialize(stream, AppSettingsJsonContext.Default.AppSettings) ?? new AppSettings();
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
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, settings, AppSettingsJsonContext.Default.AppSettings);
            }

            File.Move(tempPath, _path, overwrite: true);
        }
    }
}
