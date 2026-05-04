using WallP.Models;

namespace WallP.Services;

public sealed class SyncScheduler
{
    private readonly AppSettings _settings;
    private readonly WallhavenApiService _api;
    private readonly ImageCache _cache;
    private readonly ImageOptimizer _optimizer;

    public SyncScheduler(
        AppSettings settings,
        WallhavenApiService api,
        ImageCache cache,
        ImageOptimizer optimizer)
    {
        _settings = settings;
        _api = api;
        _cache = cache;
        _optimizer = optimizer;
    }

    public void Start() => throw new NotImplementedException();
    public void Stop() => throw new NotImplementedException();

    public Task SyncNowAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task SyncCollectionAsync(Guid collectionId, CancellationToken ct = default) => throw new NotImplementedException();
}
