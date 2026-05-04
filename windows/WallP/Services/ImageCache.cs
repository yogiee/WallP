using System.IO;
using WallP.Models;

namespace WallP.Services;

public sealed class ImageCache
{
    public static string CacheDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallP", "cache");

    private readonly AppSettings _settings;

    public ImageCache(AppSettings settings)
    {
        _settings = settings;
        Directory.CreateDirectory(CacheDirectory);
    }

    public string PathFor(CachedImage image) =>
        Path.Combine(CacheDirectory, image.CollectionId.ToString(), image.LocalFilename);

    public Task<CachedImage> StoreAsync(WallhavenWallpaper wallpaper, byte[] data, Guid collectionId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task PruneCollectionAsync(Guid collectionId, int keepLimit, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task ClearAllAsync(CancellationToken ct = default)
        => throw new NotImplementedException();

    public long TotalSizeBytes()
    {
        if (!Directory.Exists(CacheDirectory)) return 0;
        return new DirectoryInfo(CacheDirectory)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }
}
