using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Windows.Networking.Connectivity;
using WallP.Models;

namespace WallP.Services;

public sealed class SyncCompletedEventArgs : EventArgs
{
    public int NewImageCount { get; }
    public string? Error { get; }

    public SyncCompletedEventArgs(int newImageCount, string? error)
    {
        NewImageCount = newImageCount;
        Error = error;
    }
}

public sealed class SyncScheduler : INotifyPropertyChanged, IDisposable
{
    private readonly AppSettings _settings;
    private readonly WallhavenApiService _api;
    private readonly ImageCache _cache;

    private readonly Lock _lock = new();
    private Timer? _timer;
    private CancellationTokenSource? _cts;

    private bool _isSyncing;
    public bool IsSyncing
    {
        get => _isSyncing;
        private set => Set(ref _isSyncing, value);
    }

    private string? _lastSyncError;
    public string? LastSyncError
    {
        get => _lastSyncError;
        private set => Set(ref _lastSyncError, value);
    }

    private string _syncProgress = "";
    public string SyncProgress
    {
        get => _syncProgress;
        private set => Set(ref _syncProgress, value);
    }

    public event EventHandler<SyncCompletedEventArgs>? SyncCompleted;

    /// <summary>
    /// Fires after each successful per-image cache during a sync. Lets the rotator
    /// react mid-sync (auto-start on first image, refresh its list as more land).
    /// </summary>
    public event EventHandler<CachedImage>? ImageCached;

    public SyncScheduler(AppSettings settings, WallhavenApiService api, ImageCache cache)
    {
        _settings = settings;
        _api = api;
        _cache = cache;
    }

    public void Start()
    {
        ScheduleTimer();

        // Initial sync if there are collections configured but the cache is empty.
        if (_settings.Collections.Count > 0 && _settings.CachedImages.Count == 0)
        {
            _ = Task.Run(() => SyncNowAsync(CancellationToken.None));
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _cts?.Cancel();
        }
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public async Task SyncNowAsync(CancellationToken ct = default)
    {
        if (IsSyncing) return;
        if (_settings.Collections.Count == 0)
        {
            LastSyncError = "No collections configured. Add one in Settings.";
            return;
        }
        if (_settings.RespectMeteredNetwork && IsMeteredConnection())
        {
            LastSyncError = "Skipped: connection is metered.";
            return;
        }

        IsSyncing = true;
        LastSyncError = null;
        SyncProgress = "Starting sync…";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_lock) { _cts = cts; }
        var token = cts.Token;

        var totalNew = 0;
        try
        {
            // Snapshot collections so a mid-sync edit doesn't cause an iteration crash.
            var collections = _settings.Collections.ToList();
            foreach (var collection in collections)
            {
                token.ThrowIfCancellationRequested();
                SyncProgress = $"Syncing \"{collection.Name}\"…";
                totalNew += await SyncOneAsync(collection, token);
            }

            SyncProgress = $"Sync complete. {_settings.CachedImages.Count} images cached.";
            Debug.WriteLine($"[WallP][Sync] Complete. New: {totalNew}, total cached: {_settings.CachedImages.Count}");
        }
        catch (OperationCanceledException)
        {
            SyncProgress = "Sync canceled.";
        }
        catch (Exception ex)
        {
            LastSyncError = ex.Message;
            SyncProgress = "";
            Debug.WriteLine($"[WallP][Sync] Error: {ex}");
        }
        finally
        {
            IsSyncing = false;
            lock (_lock) { _cts = null; }
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(totalNew, LastSyncError));
        }
    }

    public async Task SyncCollectionAsync(Guid collectionId, CancellationToken ct = default)
    {
        if (IsSyncing) return;

        var collection = _settings.Collections.FirstOrDefault(c => c.Id == collectionId);
        if (collection is null)
        {
            LastSyncError = "Collection not found.";
            return;
        }
        if (_settings.RespectMeteredNetwork && IsMeteredConnection())
        {
            LastSyncError = "Skipped: connection is metered.";
            return;
        }

        IsSyncing = true;
        LastSyncError = null;
        SyncProgress = $"Syncing \"{collection.Name}\"…";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_lock) { _cts = cts; }
        var token = cts.Token;

        var totalNew = 0;
        try
        {
            totalNew = await SyncOneAsync(collection, token);
            SyncProgress = $"Downloaded {totalNew} new images.";
        }
        catch (OperationCanceledException)
        {
            SyncProgress = "Sync canceled.";
        }
        catch (Exception ex)
        {
            LastSyncError = ex.Message;
            SyncProgress = "";
            Debug.WriteLine($"[WallP][Sync] Error: {ex}");
        }
        finally
        {
            IsSyncing = false;
            lock (_lock) { _cts = null; }
            SyncCompleted?.Invoke(this, new SyncCompletedEventArgs(totalNew, LastSyncError));
        }
    }

    private async Task<int> SyncOneAsync(WallPCollection collection, CancellationToken ct)
    {
        Debug.WriteLine($"[WallP][Sync] Fetching list for \"{collection.Name}\" (Wallhaven #{collection.WallhavenCollectionId})");

        var wallpapers = await _api.FetchAllCollectionWallpapersAsync(
            collection.WallhavenUsername,
            collection.WallhavenCollectionId,
            maxPages: 10,
            ct);

        var existingIds = _settings.CachedImages
            .Where(i => i.CollectionId == collection.Id)
            .Select(i => i.WallhavenId)
            .ToHashSet();

        var newOnes = wallpapers.Where(w => !existingIds.Contains(w.Id)).ToList();
        var capacity = (int)_settings.CacheLimit - existingIds.Count;
        if (capacity <= 0) return 0;

        var toDownload = newOnes.Take(capacity).ToList();
        if (toDownload.Count == 0) return 0;

        Debug.WriteLine($"[WallP][Sync] Downloading {toDownload.Count} of {newOnes.Count} new (limit: {(int)_settings.CacheLimit})");

        var added = 0;
        for (var i = 0; i < toDownload.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var wallpaper = toDownload[i];
            SyncProgress = $"Syncing \"{collection.Name}\" — {i + 1}/{toDownload.Count}";

            try
            {
                var data = await _api.DownloadImageAsync(wallpaper.Path, ct);
                var cached = await _cache.StoreAsync(wallpaper, data, collection.Id, ct);
                _settings.CachedImages.Add(cached);
                collection.CachedImageIds.Add(cached.Id);
                added++;
                ImageCached?.Invoke(this, cached);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WallP][Sync] Failed {wallpaper.Id}: {ex.Message}");
                // Skip this one and continue — partial sync is better than no sync.
            }

            // Pace requests so we stay well under Wallhaven's 45/min limit, even with retries upstream.
            try { await Task.Delay(500, ct); } catch (OperationCanceledException) { throw; }
        }

        if (added > 0)
        {
            collection.LastSynced = DateTime.UtcNow;
            _settings.Save();
        }

        return added;
    }

    private void ScheduleTimer()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;

            var seconds = (int)_settings.SyncInterval;
            if (seconds <= 0) return; // Manual only — no timer.

            var period = TimeSpan.FromSeconds(seconds);
            _timer = new Timer(static state =>
            {
                var self = (SyncScheduler)state!;
                _ = self.SyncNowAsync();
            }, this, period, period);
        }
    }

    public void Dispose() => Stop();

    /// <summary>
    /// Checks the active internet connection profile via WinRT and returns true when
    /// it carries a non-Unrestricted cost (i.e., the user has flagged the network as
    /// metered or it's a known cellular / capped link). Returns false on any error so
    /// a transient connectivity hiccup never blocks sync indefinitely.
    /// </summary>
    private static bool IsMeteredConnection()
    {
        try
        {
            var profile = NetworkInformation.GetInternetConnectionProfile();
            if (profile is null) return false;
            var cost = profile.GetConnectionCost();
            return cost.NetworkCostType is NetworkCostType.Fixed or NetworkCostType.Variable;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Sync] Metered check failed: {ex.Message}");
            return false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
