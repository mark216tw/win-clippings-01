namespace Clippings.Core.Models;

public sealed class CaptureItem
{
    public int Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public int Width { get; set; }

    public int Height { get; set; }

    public string FilePath { get; set; } = string.Empty;

    public string ThumbnailPath { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public string Source { get; set; } = string.Empty;
}
