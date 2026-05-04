using WallP.Models;

namespace WallP.Services;

public sealed class ImageOptimizer
{
    private readonly AppSettings _settings;

    public ImageOptimizer(AppSettings settings)
    {
        _settings = settings;
    }

    public Task<byte[]> OptimizeAsync(byte[] sourceData, int targetWidth, int targetHeight, CancellationToken ct = default)
        => throw new NotImplementedException();

    public static bool IsHeicAvailable() => throw new NotImplementedException();
}
