using Clippings.Core.Models;

namespace Clippings.Core.Services;

public interface ISettingsStore
{
    AppSettings Load();

    void Save(AppSettings settings);
}

public interface IClippingsStore
{
    string DataRoot { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<CaptureItem> AddCaptureAsync(CaptureItem item, byte[] imageBytes, string extension, byte[] thumbnailBytes, CancellationToken cancellationToken = default);

    Task<CaptureItem?> UpdateCaptureImageAsync(int id, byte[] imageBytes, string extension, byte[] thumbnailBytes, int width, int height, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaptureItem>> GetCapturesAsync(string? search, bool favoritesOnly, int limit, CancellationToken cancellationToken = default);

    Task<CaptureItem?> GetCaptureAsync(int id, CancellationToken cancellationToken = default);

    Task DeleteCaptureAsync(int id, CancellationToken cancellationToken = default);

    Task<int> DeleteNonFavoriteCapturesAsync(CancellationToken cancellationToken = default);

    Task SetCaptureFavoriteAsync(int id, bool isFavorite, CancellationToken cancellationToken = default);

    Task<int> CountCapturesAsync(CancellationToken cancellationToken = default);

    Task TrimCapturesAsync(int maxItems, int retentionDays, CancellationToken cancellationToken = default);

    Task<ClipboardItem> AddClipboardItemAsync(ClipboardItem item, byte[]? imageBytes, byte[]? thumbnailBytes, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClipboardItem>> GetClipboardItemsAsync(string? search, ClipboardItemKind? kind, int limit, CancellationToken cancellationToken = default);

    Task<int> CountClipboardItemsAsync(CancellationToken cancellationToken = default);

    Task DeleteClipboardItemAsync(int id, CancellationToken cancellationToken = default);

    Task SetClipboardPinnedAsync(int id, bool isPinned, CancellationToken cancellationToken = default);

    Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default);

    Task TrimClipboardItemsAsync(int maxItems, int retentionDays, CancellationToken cancellationToken = default);
}
