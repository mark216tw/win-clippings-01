namespace Clippings.Core.Models;

public enum ClipboardItemKind
{
    Text,
    Image,
    Files,
}

public sealed class ClipboardItem
{
    public int Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public ClipboardItemKind Kind { get; set; }

    public string? TextContent { get; set; }

    public string? FilePath { get; set; }

    public string? ThumbnailPath { get; set; }

    public string Hash { get; set; } = string.Empty;

    public bool IsPinned { get; set; }
}
