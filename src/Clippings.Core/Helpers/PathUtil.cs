namespace Clippings.Core.Helpers;

public static class PathUtil
{
    public const string AppFolderName = "Clippings";

    public static string DefaultDataRoot
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

    public static string DefaultSaveRoot
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            AppFolderName);

    public static string DefaultRecordingRoot
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            AppFolderName);

    public static string ImagesDirectory(string dataRoot) => Path.Combine(dataRoot, "images");

    public static string ThumbnailsDirectory(string dataRoot) => Path.Combine(dataRoot, "thumbs");

    public static string ClipboardDirectory(string dataRoot) => Path.Combine(dataRoot, "clipboard");

    public static string DatabasePath(string dataRoot) => Path.Combine(dataRoot, "clippings.db");

    public static string SettingsPath(string dataRoot) => Path.Combine(dataRoot, "settings.json");

    public static string LogDirectory(string dataRoot) => Path.Combine(dataRoot, "logs");

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    public static string BuildImageFileName(DateTimeOffset createdAt, string hash, string extension)
        => $"Clippings_{createdAt:yyyyMMdd_HHmmss}_{hash[..8]}.{extension}";

    public static string BuildRecordingFileName(DateTimeOffset createdAt)
        => $"Clippings_{createdAt:yyyyMMdd_HHmmss}.mp4";
}
