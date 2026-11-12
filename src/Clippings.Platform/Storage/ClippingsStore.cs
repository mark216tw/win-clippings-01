using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace Clippings.Platform.Storage;

public sealed class ClippingsStore : IClippingsStore
{
    private readonly string _dataRoot;
    private readonly DbContextOptions<ClippingsDbContext> _options;
    private readonly object _initializationGate = new();
    private Task? _initializationTask;

    public ClippingsStore()
        : this(PathUtil.DefaultDataRoot)
    {
    }

    public ClippingsStore(string dataRoot)
    {
        _dataRoot = dataRoot;
        _options = new DbContextOptionsBuilder<ClippingsDbContext>()
            .UseSqlite($"Data Source={PathUtil.DatabasePath(_dataRoot)};Default Timeout=5;Pooling=True")
            .AddInterceptors(new SqlitePragmaInterceptor())
            .Options;
    }

    public string DataRoot => _dataRoot;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var task = Volatile.Read(ref _initializationTask);
        if (task is null)
        {
            lock (_initializationGate)
            {
                task = _initializationTask;
                if (task is null)
                {
                    task = Task.Run(InitializeCoreAsync);
                    _initializationTask = task;
                }
            }
        }

        return cancellationToken.CanBeCanceled ? task.WaitAsync(cancellationToken) : task;
    }

    private async Task InitializeCoreAsync()
    {
        Directory.CreateDirectory(_dataRoot);
        Directory.CreateDirectory(PathUtil.ImagesDirectory(_dataRoot));
        Directory.CreateDirectory(PathUtil.ThumbnailsDirectory(_dataRoot));
        Directory.CreateDirectory(PathUtil.ClipboardDirectory(_dataRoot));
        Directory.CreateDirectory(PathUtil.LogDirectory(_dataRoot));

        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;").ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Captures_IsFavorite_CreatedAt ON Captures (IsFavorite, CreatedAt);")
            .ConfigureAwait(false);
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_ClipboardItems_IsPinned_CreatedAt ON ClipboardItems (IsPinned, CreatedAt);")
            .ConfigureAwait(false);
    }

    public async Task<CaptureItem> AddCaptureAsync(
        CaptureItem item,
        byte[] imageBytes,
        string extension,
        byte[] thumbnailBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(thumbnailBytes);

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(item.Hash))
        {
            item.Hash = HashUtil.Sha256Hex(imageBytes);
        }

        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? "png"
            : extension.TrimStart('.').ToLowerInvariant();

        var fileName = PathUtil.BuildImageFileName(item.CreatedAt, item.Hash, normalizedExtension);
        var imageDirectory = PathUtil.ImagesDirectory(_dataRoot);
        var thumbDirectory = PathUtil.ThumbnailsDirectory(_dataRoot);

        var imagePath = Path.Combine(imageDirectory, fileName);
        var thumbPath = Path.Combine(thumbDirectory, Path.ChangeExtension(fileName, ".jpg"));

        await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(thumbPath, thumbnailBytes, cancellationToken).ConfigureAwait(false);

        item.FilePath = imagePath;
        item.ThumbnailPath = thumbPath;

        await using var db = CreateContext();
        var entity = ToEntity(item);
        db.Captures.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        item.Id = entity.Id;

        return item;
    }

    public async Task<CaptureItem?> UpdateCaptureImageAsync(
        int id,
        byte[] imageBytes,
        string extension,
        byte[] thumbnailBytes,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);
        ArgumentNullException.ThrowIfNull(thumbnailBytes);

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var entity = await db.Captures
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return null;
        }

        var createdAt = DateTimeOffset.Now;
        var hash = HashUtil.Sha256Hex(imageBytes);
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? "png"
            : extension.TrimStart('.').ToLowerInvariant();

        var imagePath = entity.FilePath;
        var thumbPath = entity.ThumbnailPath;
        if (string.IsNullOrWhiteSpace(imagePath) || string.IsNullOrWhiteSpace(thumbPath) ||
            !File.Exists(imagePath) || !File.Exists(thumbPath))
        {
            var fileName = PathUtil.BuildImageFileName(createdAt, hash, normalizedExtension);
            imagePath = Path.Combine(PathUtil.ImagesDirectory(_dataRoot), fileName);
            thumbPath = Path.Combine(PathUtil.ThumbnailsDirectory(_dataRoot), Path.ChangeExtension(fileName, ".jpg"));
        }

        var imageDirectory = Path.GetDirectoryName(imagePath);
        if (!string.IsNullOrEmpty(imageDirectory))
        {
            Directory.CreateDirectory(imageDirectory);
        }

        var thumbDirectory = Path.GetDirectoryName(thumbPath);
        if (!string.IsNullOrEmpty(thumbDirectory))
        {
            Directory.CreateDirectory(thumbDirectory);
        }

        await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(thumbPath, thumbnailBytes, cancellationToken).ConfigureAwait(false);

        entity.FilePath = imagePath;
        entity.ThumbnailPath = thumbPath;
        entity.Hash = hash;
        entity.Width = width;
        entity.Height = height;
        entity.CreatedAt = createdAt.ToUniversalTime().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToModel(entity);
    }

    public async Task<IReadOnlyList<CaptureItem>> GetCapturesAsync(
        string? search,
        bool favoritesOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var query = db.Captures.AsNoTracking();

        if (favoritesOnly)
        {
            query = query.Where(item => item.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.Like(item.Source, pattern) ||
                EF.Functions.Like(item.FilePath, pattern));
        }

        var entities = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit, 1, 5000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToModel).ToList();
    }

    public async Task<CaptureItem?> GetCaptureAsync(int id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var entity = await db.Captures
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToModel(entity);
    }

    public async Task DeleteCaptureAsync(int id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var entity = await db.Captures
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        db.Captures.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        TryDeleteFile(entity.FilePath);
        TryDeleteFile(entity.ThumbnailPath);
    }

    public async Task<int> DeleteNonFavoriteCapturesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var targets = await db.Captures
            .Where(item => !item.IsFavorite)
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (targets.Count == 0)
        {
            return 0;
        }

        var ids = targets.Select(item => item.Id).ToList();
        var deleted = await db.Captures
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var target in targets)
        {
            TryDeleteFile(target.FilePath);
            TryDeleteFile(target.ThumbnailPath);
        }

        return deleted;
    }

    public async Task SetCaptureFavoriteAsync(int id, bool isFavorite, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var entity = await db.Captures
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.IsFavorite = isFavorite;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountCapturesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        return await db.Captures.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountClipboardItemsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        return await db.ClipboardItems.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task TrimCapturesAsync(int maxItems, int retentionDays, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, retentionDays));
        var stale = await db.Captures
            .Where(item => !item.IsFavorite && item.CreatedAt < cutoff)
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var overflow = await db.Captures
            .Where(item => !item.IsFavorite)
            .OrderByDescending(item => item.CreatedAt)
            .Skip(Math.Max(0, maxItems))
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targets = stale.Concat(overflow).DistinctBy(item => item.Id).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var ids = targets.Select(item => item.Id).ToList();
        await db.Captures
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var target in targets)
        {
            TryDeleteFile(target.FilePath);
            TryDeleteFile(target.ThumbnailPath);
        }
    }

    public async Task<ClipboardItem> AddClipboardItemAsync(
        ClipboardItem item,
        byte[]? imageBytes,
        byte[]? thumbnailBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var existing = await db.ClipboardItems
            .FirstOrDefaultAsync(row => row.Hash == item.Hash, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            existing.CreatedAt = DateTime.UtcNow;
            existing.TextContent = item.TextContent ?? existing.TextContent;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToModel(existing);
        }

        if (imageBytes is not null)
        {
            var fileName = PathUtil.BuildImageFileName(item.CreatedAt, item.Hash, "png");
            var imagePath = Path.Combine(PathUtil.ClipboardDirectory(_dataRoot), fileName);
            await File.WriteAllBytesAsync(imagePath, imageBytes, cancellationToken).ConfigureAwait(false);
            item.FilePath = imagePath;

            if (thumbnailBytes is not null)
            {
                var thumbnailPath = Path.Combine(PathUtil.ThumbnailsDirectory(_dataRoot), $"clip_{Path.ChangeExtension(fileName, ".jpg")}");
                await File.WriteAllBytesAsync(thumbnailPath, thumbnailBytes, cancellationToken).ConfigureAwait(false);
                item.ThumbnailPath = thumbnailPath;
            }
        }

        var entity = ToEntity(item);
        db.ClipboardItems.Add(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        item.Id = entity.Id;
        return item;
    }

    public async Task<IReadOnlyList<ClipboardItem>> GetClipboardItemsAsync(
        string? search,
        ClipboardItemKind? kind,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var query = db.ClipboardItems.AsNoTracking();

        if (kind is { } kindValue)
        {
            query = query.Where(item => item.Kind == kindValue);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(item => item.TextContent != null && EF.Functions.Like(item.TextContent, pattern));
        }

        var entities = await query
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit, 1, 5000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToModel).ToList();
    }

    public async Task DeleteClipboardItemAsync(int id, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var entity = await db.ClipboardItems
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        db.ClipboardItems.Remove(entity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        TryDeleteFile(entity.FilePath);
        TryDeleteFile(entity.ThumbnailPath);
    }

    public async Task SetClipboardPinnedAsync(int id, bool isPinned, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var entity = await db.ClipboardItems
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return;
        }

        entity.IsPinned = isPinned;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearClipboardItemsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var items = await db.ClipboardItems
            .Where(item => !item.IsPinned)
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await db.ClipboardItems
            .Where(item => !item.IsPinned)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var item in items)
        {
            TryDeleteFile(item.FilePath);
            TryDeleteFile(item.ThumbnailPath);
        }
    }

    public async Task TrimClipboardItemsAsync(int maxItems, int retentionDays, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var db = CreateContext();
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, retentionDays));
        var stale = await db.ClipboardItems
            .Where(item => !item.IsPinned && item.CreatedAt < cutoff)
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var overflow = await db.ClipboardItems
            .Where(item => !item.IsPinned)
            .OrderByDescending(item => item.CreatedAt)
            .Skip(Math.Max(0, maxItems))
            .Select(item => new TrimTarget(item.Id, item.FilePath, item.ThumbnailPath))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var targets = stale.Concat(overflow).DistinctBy(item => item.Id).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var ids = targets.Select(item => item.Id).ToList();
        await db.ClipboardItems
            .Where(item => ids.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var target in targets)
        {
            TryDeleteFile(target.FilePath);
            TryDeleteFile(target.ThumbnailPath);
        }
    }

    private ClippingsDbContext CreateContext() => new(_options);

    private readonly record struct TrimTarget(int Id, string? FilePath, string? ThumbnailPath);

    private static CaptureEntity ToEntity(CaptureItem item) => new()
    {
        Id = item.Id,
        CreatedAt = item.CreatedAt.ToUniversalTime().UtcDateTime,
        Width = item.Width,
        Height = item.Height,
        FilePath = item.FilePath,
        ThumbnailPath = item.ThumbnailPath,
        Hash = item.Hash,
        IsFavorite = item.IsFavorite,
        Source = item.Source,
    };

    private static CaptureItem ToModel(CaptureEntity entity) => new()
    {
        Id = entity.Id,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)).ToLocalTime(),
        Width = entity.Width,
        Height = entity.Height,
        FilePath = entity.FilePath,
        ThumbnailPath = entity.ThumbnailPath,
        Hash = entity.Hash,
        IsFavorite = entity.IsFavorite,
        Source = entity.Source,
    };

    private static ClipboardEntity ToEntity(ClipboardItem item) => new()
    {
        Id = item.Id,
        CreatedAt = item.CreatedAt.ToUniversalTime().UtcDateTime,
        Kind = item.Kind,
        TextContent = item.TextContent,
        FilePath = item.FilePath,
        ThumbnailPath = item.ThumbnailPath,
        Hash = item.Hash,
        IsPinned = item.IsPinned,
    };

    private static ClipboardItem ToModel(ClipboardEntity entity) => new()
    {
        Id = entity.Id,
        CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)).ToLocalTime(),
        Kind = entity.Kind,
        TextContent = entity.TextContent,
        FilePath = entity.FilePath,
        ThumbnailPath = entity.ThumbnailPath,
        Hash = entity.Hash,
        IsPinned = entity.IsPinned,
    };

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}



