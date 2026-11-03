using Clippings.Core.Models;
using Clippings.Core.Services;

namespace Clippings.App.Services;

public sealed class AppState
{
    private readonly ISettingsStore _store;

    public AppState(ISettingsStore store)
    {
        _store = store;
        Settings = store.Load();
    }

    public AppSettings Settings { get; private set; }

    public event EventHandler? SettingsChanged;

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        _store.Save(settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RememberAnnotation(AnnotationSettings annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        Settings.Annotation = annotation;
        _store.Save(Settings);
    }
}
