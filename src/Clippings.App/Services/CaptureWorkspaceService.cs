using System.Buffers.Binary;
using System.Collections.ObjectModel;
using Clippings.App.Models;
using Clippings.App.Strings;
using Clippings.Core.Helpers;
using Clippings.Core.Models;
using Clippings.Core.Services;
using Clippings.Platform.Imaging;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Clippings.App.Services;

public sealed record WorkspaceAddResult(WorkspaceTab? Tab, bool LimitReached)
{
    public bool Succeeded => Tab is not null;
}

public sealed class CaptureWorkspaceService
{
    private readonly ILogger<CaptureWorkspaceService> _logger;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly string _directory;
    private bool _loaded;
    private int _nextTitleNumber = 1;

    public CaptureWorkspaceService(ILogger<CaptureWorkspaceService> logger)
    {
        _logger = logger;
        _directory = PathUtil.WorkspaceDirectory(PathUtil.DefaultDataRoot);
    }

    public ObservableCollection<WorkspaceTab> Tabs { get; } = [];

    public string WorkspaceDirectory => _directory;

    public async Task LoadAsync()
    {
        await _ioLock.WaitAsync();
        try
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            Directory.CreateDirectory(_directory);
            DeleteTemporaryFiles();

            var files = Directory
                .EnumerateFiles(_directory, "*.png")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count > CaptureWorkspaceLimits.MaxTabs)
            {
                foreach (var overflow in files.Take(files.Count - CaptureWorkspaceLimits.MaxTabs))
                {
                    TryDeleteFile(overflow);
                }

                files = files.Skip(files.Count - CaptureWorkspaceLimits.MaxTabs).ToList();
            }

            var loaded = await Task.Run(() =>
            {
                var items = new List<(string Path, int Width, int Height)>();
                foreach (var path in files)
                {
                    try
                    {
                        if (!TryReadPngDimensions(path, out var width, out var height))
                        {
                            throw new InvalidOperationException("無法讀取影像資訊。");
                        }

                        items.Add((path, width, height));
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "略過無法讀取的工作區暫存檔：{Path}", path);
                        TryDeleteFile(path);
                    }
                }

                return items;
            });

            foreach (var (path, width, height) in loaded)
            {
                Tabs.Add(new WorkspaceTab
                {
                    CreatedAt = new DateTimeOffset(File.GetCreationTime(path)),
                    Title = AppStrings.FormatWorkspaceTabTitle(_nextTitleNumber++),
                    Width = width,
                    Height = height,
                    TempPath = path,
                });
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "讀取擷圖工作區暫存檔失敗。");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<WorkspaceAddResult> AddTabAsync(byte[] pngBytes, int width, int height, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        await LoadAsync();
        await _ioLock.WaitAsync();
        string? path = null;
        try
        {
            if (!CaptureWorkspaceLimits.CanAddTab(Tabs.Count))
            {
                return new WorkspaceAddResult(null, LimitReached: true);
            }

            var createdAt = DateTimeOffset.Now;
            path = CreateTempPath(createdAt);
            await File.WriteAllBytesAsync(path, pngBytes);

            var tab = new WorkspaceTab
            {
                CreatedAt = createdAt,
                Title = string.IsNullOrWhiteSpace(title)
                    ? AppStrings.FormatWorkspaceTabTitle(_nextTitleNumber++)
                    : title.Trim(),
                Width = width,
                Height = height,
                TempPath = path,
            };
            Tabs.Add(tab);
            return new WorkspaceAddResult(tab, LimitReached: false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "建立擷圖工作區暫存檔失敗。");
            if (path is not null)
            {
                TryDeleteFile(path);
            }

            return new WorkspaceAddResult(null, LimitReached: false);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task<WorkspaceAddResult> AddTabAsync(SKBitmap bitmap, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var bytes = await Task.Run(() => ImageCodec.Encode(bitmap, ImageFormat.Png));
        return await AddTabAsync(bytes, bitmap.Width, bitmap.Height, title);
    }

    public async Task<bool> UpdateTempAsync(WorkspaceTab tab, byte[] pngBytes, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(pngBytes);

        await _ioLock.WaitAsync();
        string? path = null;
        try
        {
            path = string.IsNullOrWhiteSpace(tab.TempPath) || !File.Exists(tab.TempPath)
                ? CreateTempPath(tab.CreatedAt)
                : tab.TempPath;

            var temporaryPath = path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, pngBytes);
            File.Move(temporaryPath, path, overwrite: true);

            tab.TempPath = path;
            tab.Width = width;
            tab.Height = height;
            tab.MemoryImage = null;
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "寫入擷圖工作區暫存檔失敗。");
            return false;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void DetachTab(WorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        Tabs.Remove(tab);
    }

    public void DetachAll() => Tabs.Clear();

    public bool TryRestoreTab(WorkspaceTab tab, int index)
    {
        ArgumentNullException.ThrowIfNull(tab);
        if (!CaptureWorkspaceLimits.CanAddTab(Tabs.Count))
        {
            return false;
        }

        Tabs.Insert(Math.Clamp(index, 0, Tabs.Count), tab);
        return true;
    }

    public async Task DeleteTabAsync(WorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        DetachTab(tab);
        await DeleteDetachedTabAsync(tab);
    }

    public async Task DeleteDetachedTabAsync(WorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        await _ioLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(tab.TempPath))
            {
                TryDeleteFile(tab.TempPath);
            }

            tab.TempPath = null;
            tab.MemoryImage = null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public async Task DeleteAllAsync()
    {
        var tabs = Tabs.ToList();
        DetachAll();

        foreach (var tab in tabs)
        {
            await DeleteDetachedTabAsync(tab);
        }
    }

    public async Task DeleteTempAfterSaveAsync(WorkspaceTab tab)
    {
        ArgumentNullException.ThrowIfNull(tab);

        await _ioLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(tab.TempPath))
            {
                TryDeleteFile(tab.TempPath);
            }

            tab.TempPath = null;
        }
        finally
        {
            _ioLock.Release();
        }
    }

    private string CreateTempPath(DateTimeOffset createdAt)
        => Path.Combine(
            _directory,
            PathUtil.BuildWorkspaceFileName(createdAt, Guid.NewGuid().ToString("N")[..8]));

    private void DeleteTemporaryFiles()
    {
        foreach (var path in Directory.EnumerateFiles(_directory, "*.tmp"))
        {
            TryDeleteFile(path);
        }
    }

    private static bool TryReadPngDimensions(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        Span<byte> header = stackalloc byte[24];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64);
        var read = 0;
        while (read < header.Length)
        {
            var chunk = stream.Read(header[read..]);
            if (chunk == 0)
            {
                return false;
            }

            read += chunk;
        }

        // PNG signature (8 bytes) + IHDR length (4 bytes) + "IHDR" (4 bytes) + width (4) + height (4)
        if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47 ||
            header[4] != 0x0D || header[5] != 0x0A || header[6] != 0x1A || header[7] != 0x0A ||
            header[12] != (byte)'I' || header[13] != (byte)'H' || header[14] != (byte)'D' || header[15] != (byte)'R')
        {
            return false;
        }

        width = BinaryPrimitives.ReadInt32BigEndian(header.Slice(16, 4));
        height = BinaryPrimitives.ReadInt32BigEndian(header.Slice(20, 4));
        return width > 0 && height > 0;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "刪除工作區暫存檔失敗：{Path}", path);
        }
    }
}
