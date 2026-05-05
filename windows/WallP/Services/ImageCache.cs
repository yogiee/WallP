using System.IO;
using WallP.Models;

namespace WallP.Services;

public sealed class ImageCache
{
    public static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallP", "cache");

    private readonly AppSettings _settings;
    private readonly WallhavenApiService _api;
    private readonly ImageOptimizer _optimizer;

    public ImageCache(AppSettings settings, WallhavenApiService api, ImageOptimizer optimizer)
    {
        _settings = settings;
        _api = api;
        _optimizer = optimizer;
        Directory.CreateDirectory(CacheDirectory);
    }

    public string PathFor(CachedImage image) =>
        Path.Combine(CacheDirectory, image.CollectionId.ToString(), image.LocalFilename);

    /// <summary>
    /// Downloads + (optionally) optimizes a single Wallhaven image into the cache,
    /// returning the metadata to add to <see cref="AppSettings.CachedImages"/>.
    /// Caller is responsible for appending the result and saving settings.
    /// </summary>
    public async Task<CachedImage> StoreAsync(
        WallhavenWallpaper wallpaper,
        byte[] data,
        Guid collectionId,
        CancellationToken ct = default)
    {
        var collectionDir = Path.Combine(CacheDirectory, collectionId.ToString());
        Directory.CreateDirectory(collectionDir);

        var sourceExtension = ExtensionFromUrl(wallpaper.Path);
        var tempPath = Path.Combine(collectionDir, $"{wallpaper.Id}_original{sourceExtension}");
        await File.WriteAllBytesAsync(tempPath, data, ct);

        string finalPath;
        if (_settings.OptimizeImages)
        {
            try
            {
                finalPath = await _optimizer.OptimizeAsync(tempPath, collectionDir, $"{wallpaper.Id}_opt", ct);
            }
            finally
            {
                TryDelete(tempPath);
            }
        }
        else
        {
            var keepPath = Path.Combine(collectionDir, $"{wallpaper.Id}{sourceExtension}");
            if (File.Exists(keepPath)) File.Delete(keepPath);
            File.Move(tempPath, keepPath);
            finalPath = keepPath;
        }

        var fileSize = new FileInfo(finalPath).Length;

        return new CachedImage
        {
            Id = wallpaper.Id,
            WallhavenId = wallpaper.Id,
            OriginalUrl = wallpaper.Path,
            LocalFilename = Path.GetFileName(finalPath),
            Width = wallpaper.DimensionX,
            Height = wallpaper.DimensionY,
            FileSize = fileSize,
            DateAdded = DateTime.UtcNow,
            CollectionId = collectionId,
        };
    }

    /// <summary>
    /// Trims a collection's cached images to <paramref name="keepLimit"/>, deleting the
    /// oldest excess files from disk and removing them from settings. Saves settings.
    /// </summary>
    public Task PruneCollectionAsync(Guid collectionId, int keepLimit, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var images = _settings.CachedImages
                .Where(i => i.CollectionId == collectionId)
                .OrderByDescending(i => i.DateAdded)
                .ToList();

            if (images.Count <= keepLimit) return;

            foreach (var image in images.Skip(keepLimit))
            {
                ct.ThrowIfCancellationRequested();
                TryDelete(PathFor(image));
                _settings.CachedImages.Remove(image);
            }

            var collection = _settings.Collections.FirstOrDefault(c => c.Id == collectionId);
            if (collection is not null)
            {
                collection.CachedImageIds = images.Take(keepLimit).Select(i => i.Id).ToList();
            }

            _settings.Save();
        }, ct);

    /// <summary>Deletes the on-disk cache directory for a single collection.</summary>
    public Task ClearCollectionAsync(Guid collectionId, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var dir = Path.Combine(CacheDirectory, collectionId.ToString());
            if (Directory.Exists(dir))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch { /* best-effort — let it fail silently */ }
            }

            _settings.CachedImages.RemoveAll(i => i.CollectionId == collectionId);
            var collection = _settings.Collections.FirstOrDefault(c => c.Id == collectionId);
            if (collection is not null) collection.CachedImageIds.Clear();
            _settings.Save();
        }, ct);

    /// <summary>Deletes the entire on-disk cache and clears all cached-image metadata.</summary>
    public Task ClearAllAsync(CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (Directory.Exists(CacheDirectory))
            {
                try { Directory.Delete(CacheDirectory, recursive: true); }
                catch { /* best-effort */ }
                Directory.CreateDirectory(CacheDirectory);
            }

            _settings.CachedImages.Clear();
            foreach (var c in _settings.Collections) c.CachedImageIds.Clear();
            _settings.Save();
        }, ct);

    public long TotalSizeBytes()
    {
        if (!Directory.Exists(CacheDirectory)) return 0;
        return new DirectoryInfo(CacheDirectory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    public string FormattedSize()
    {
        var bytes = TotalSizeBytes();
        return bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0 / 1024.0:0.#} GB",
            >= 1024L * 1024 => $"{bytes / 1024.0 / 1024.0:0.#} MB",
            >= 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes} B",
        };
    }

    private static string ExtensionFromUrl(string url)
    {
        try
        {
            var path = new Uri(url).LocalPath;
            var ext = Path.GetExtension(path);
            return string.IsNullOrEmpty(ext) ? ".jpg" : ext;
        }
        catch
        {
            return ".jpg";
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort */ }
    }
}
