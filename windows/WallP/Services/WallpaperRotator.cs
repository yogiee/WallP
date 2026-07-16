using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using WallP.Models;

namespace WallP.Services;

public sealed class WallpaperRotator : INotifyPropertyChanged, IDisposable
{
    private readonly AppSettings _settings;
    private readonly ImageCache _cache;
    private readonly DesktopWallpaperService _wallpaper;

    private readonly Lock _lock = new();
    private readonly Random _random = new();
    private Timer? _timer;
    private List<CachedImage> _orderedImages = [];
    private List<int> _shuffledIndices = [];
    private int _currentIndex = -1;

    // What's currently shown on each monitor — populated by Next/Previous and reused
    // by ReapplyCurrentAsync so we can re-blur without changing the image.
    private List<(string MonitorId, CachedImage Image)> _monitorAssignments = [];

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set => Set(ref _isRunning, value);
    }

    private string? _currentImageId;
    public string? CurrentImageId
    {
        get => _currentImageId;
        private set => Set(ref _currentImageId, value);
    }

    public WallpaperRotator(AppSettings settings, ImageCache cache, DesktopWallpaperService wallpaper)
    {
        _settings = settings;
        _cache = cache;
        _wallpaper = wallpaper;
    }

    public void Start()
    {
        if (_settings.IsPaused) return;
        IsRunning = true;
        RefreshImageList();
        ScheduleTimer();
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
        IsRunning = false;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Pause()
    {
        _settings.IsPaused = true;
        Stop();
    }

    public void Resume()
    {
        _settings.IsPaused = false;
        Start();
    }

    public void SwitchToCollection(Guid collectionId)
    {
        _settings.DefaultCollectionId = collectionId;
        RefreshImageList();
        lock (_lock) { _currentIndex = -1; }
        _ = NextWallpaperAsync();
        if (IsRunning) ScheduleTimer();
    }

    public void RefreshImageList()
    {
        lock (_lock) { RefreshImageListLocked(); }
    }

    /// <summary>
    /// Refreshes the in-memory image list from settings. Caller must already hold
    /// <see cref="_lock"/> — System.Threading.Lock is non-reentrant, so calling the
    /// public RefreshImageList from another locked region would deadlock.
    /// </summary>
    private void RefreshImageListLocked()
    {
        var collection = _settings.ActiveCollection;
        if (collection is null)
        {
            _orderedImages = [];
            _shuffledIndices = [];
            return;
        }

        var images = _settings.CachedImages
            .Where(i => i.CollectionId == collection.Id)
            .ToList();

        switch (_settings.DisplayOrder)
        {
            case DisplayOrder.Name:
                images.Sort((a, b) =>
                    string.Compare(a.LocalFilename, b.LocalFilename, StringComparison.OrdinalIgnoreCase));
                break;
            case DisplayOrder.DateCreated:
                images.Sort((a, b) => a.DateAdded.CompareTo(b.DateAdded));
                break;
            case DisplayOrder.Random:
                // Order doesn't matter; _shuffledIndices drives randomization.
                break;
        }

        // Preserve existing shuffled indices when new images land mid-sync, so we
        // don't re-shuffle on every tick. Just append fresh indices for the new entries.
        var previousCount = _orderedImages.Count;
        _orderedImages = images;
        if (_shuffledIndices.Count == 0 || images.Count < previousCount)
        {
            ReshuffleIndices();
        }
        else if (images.Count > previousCount)
        {
            for (var i = previousCount; i < images.Count; i++) _shuffledIndices.Add(i);
        }
    }

    private void ReshuffleIndices()
    {
        _shuffledIndices = Enumerable.Range(0, _orderedImages.Count).ToList();
        // Fisher-Yates
        for (var i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            var j = _random.Next(0, i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }
    }

    /// <summary>
    /// Advances to the next image index according to the active display order.
    /// Caller must already hold <see cref="_lock"/>, and <see cref="_orderedImages"/>
    /// must be non-empty.
    /// </summary>
    private int NextOrderedIndexLocked()
    {
        if (_settings.DisplayOrder == DisplayOrder.Random)
        {
            if (_shuffledIndices.Count == 0) ReshuffleIndices();
            var index = _shuffledIndices[0];
            _shuffledIndices.RemoveAt(0);
            return index;
        }
        return (_currentIndex + 1) % _orderedImages.Count;
    }

    /// <summary>
    /// Applies one image across every monitor in a single COM call, blurring once
    /// rather than once per monitor. Records one assignment per monitor so
    /// <see cref="ReapplyCurrentAsync"/> can rebuild the blur paths consistently.
    /// </summary>
    private async Task ApplyToAllMonitorsAsync(
        CachedImage image, IReadOnlyList<MonitorInfo> monitors, CancellationToken ct)
    {
        var sourcePath = _cache.PathFor(image);
        if (!File.Exists(sourcePath))
        {
            Debug.WriteLine($"[WallP][Rotator] Missing on disk, skipping: {sourcePath}");
            return;
        }

        try
        {
            var displayPath = await PrepareForDisplayAsync(sourcePath, monitorIndex: 0, ct);
            await _wallpaper.SetWallpaperAllMonitorsAsync(displayPath, ct);
            var newAssignments = monitors.Select(m => (m.Id, image)).ToList();
            lock (_lock) { _monitorAssignments = newAssignments; }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Rotator] SetWallpaper (all monitors) error: {ex.Message}");
        }
    }

    public async Task NextWallpaperAsync(CancellationToken ct = default)
    {
        IReadOnlyList<MonitorInfo> monitors;
        try { monitors = _wallpaper.GetMonitors(); }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Rotator] GetMonitors failed: {ex.Message}");
            return;
        }

        if (monitors.Count == 0) return;

        // Set when a single image is mirrored across every monitor; stays null when
        // each monitor gets its own image.
        CachedImage? mirroredImage = null;
        List<(MonitorInfo Monitor, CachedImage Image)> assignments = [];
        lock (_lock)
        {
            // Always pull the latest cached-image set — sync may have added images
            // since the last RefreshImageList call, and we want shuffle/auto-rotate
            // to reach those without waiting for SyncCompleted.
            RefreshImageListLocked();

            if (_orderedImages.Count == 0) return;

            if (monitors.Count > 1 && _settings.MultiMonitorMode == MultiMonitorMode.DifferentPerMonitor)
            {
                // Multi-monitor: unique random images when we have enough; allow repeats otherwise.
                int[] indices;
                if (_orderedImages.Count >= monitors.Count)
                {
                    indices = Enumerable.Range(0, _orderedImages.Count)
                        .OrderBy(_ => _random.Next())
                        .Take(monitors.Count)
                        .ToArray();
                }
                else
                {
                    indices = Enumerable.Range(0, monitors.Count)
                        .Select(_ => _random.Next(0, _orderedImages.Count))
                        .ToArray();
                }

                assignments = monitors.Select((m, i) => (m, _orderedImages[indices[i]])).ToList();

                // Track the primary monitor's image as the canonical "current" for UI.
                var primaryIdx = 0;
                for (var i = 0; i < monitors.Count; i++)
                {
                    if (monitors[i].IsPrimary) { primaryIdx = i; break; }
                }
                _currentIndex = indices[primaryIdx];
            }
            else
            {
                // Single monitor, or "same image on all displays" — pick one image
                // using the display-order setting and apply it to every monitor.
                _currentIndex = NextOrderedIndexLocked();
                mirroredImage = _orderedImages[_currentIndex];
            }

            CurrentImageId = _orderedImages[_currentIndex].Id;
        }

        if (mirroredImage is not null)
        {
            await ApplyToAllMonitorsAsync(mirroredImage, monitors, ct);
            return;
        }

        var newAssignments = new List<(string, CachedImage)>(assignments.Count);
        for (var i = 0; i < assignments.Count; i++)
        {
            var (monitor, image) = assignments[i];
            ct.ThrowIfCancellationRequested();
            try
            {
                var sourcePath = _cache.PathFor(image);
                if (!File.Exists(sourcePath))
                {
                    Debug.WriteLine($"[WallP][Rotator] Missing on disk, skipping: {sourcePath}");
                    continue;
                }
                var displayPath = await PrepareForDisplayAsync(sourcePath, i, ct);
                await _wallpaper.SetWallpaperAsync(monitor.Id, displayPath, ct);
                newAssignments.Add((monitor.Id, image));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WallP][Rotator] SetWallpaper error on {monitor.Id}: {ex.Message}");
            }
        }

        lock (_lock) { _monitorAssignments = newAssignments; }
    }

    public async Task PreviousWallpaperAsync(CancellationToken ct = default)
    {
        CachedImage image;
        lock (_lock)
        {
            if (_orderedImages.Count == 0) return;
            _currentIndex = _currentIndex > 0 ? _currentIndex - 1 : _orderedImages.Count - 1;
            image = _orderedImages[_currentIndex];
            CurrentImageId = image.Id;
        }

        IReadOnlyList<MonitorInfo> monitors;
        try { monitors = _wallpaper.GetMonitors(); }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Rotator] Previous: GetMonitors failed: {ex.Message}");
            return;
        }

        // Previous always mirrors one image across every monitor, regardless of
        // MultiMonitorMode — there's no per-monitor history to step back through.
        await ApplyToAllMonitorsAsync(image, monitors, ct);
    }

    public Task ShuffleAsync(CancellationToken ct = default) => NextWallpaperAsync(ct);

    /// <summary>
    /// Re-applies the wallpaper currently on each monitor without changing the image.
    /// Used when display-time settings (blur) change so the user sees the effect on
    /// the same image rather than waiting for the next rotation tick.
    /// </summary>
    public async Task ReapplyCurrentAsync(CancellationToken ct = default)
    {
        List<(string MonitorId, CachedImage Image)> snapshot;
        lock (_lock)
        {
            if (_monitorAssignments.Count == 0) return;
            snapshot = _monitorAssignments.ToList();
        }

        for (var i = 0; i < snapshot.Count; i++)
        {
            var (monitorId, image) = snapshot[i];
            ct.ThrowIfCancellationRequested();
            try
            {
                var sourcePath = _cache.PathFor(image);
                if (!File.Exists(sourcePath)) continue;
                var displayPath = await PrepareForDisplayAsync(sourcePath, i, ct);
                await _wallpaper.SetWallpaperAsync(monitorId, displayPath, ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WallP][Rotator] Reapply error on {monitorId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// If blur is on, blurs <paramref name="sourcePath"/> into a per-monitor temp file
    /// inside the cache's .blur subdirectory and returns that path. Otherwise returns
    /// <paramref name="sourcePath"/> unchanged.
    /// </summary>
    private async Task<string> PrepareForDisplayAsync(string sourcePath, int monitorIndex, CancellationToken ct)
    {
        var radius = _settings.BlurRadius;
        if (radius <= 0) return sourcePath;

        var blurDir = Path.Combine(ImageCache.CacheDirectory, ".blur");
        Directory.CreateDirectory(blurDir);
        var ext = Path.GetExtension(sourcePath);
        var blurPath = Path.Combine(blurDir, $"m{monitorIndex}{ext}");

        using var img = await Image.LoadAsync(sourcePath, ct);
        img.Mutate(x => x.GaussianBlur(radius));
        await img.SaveAsync(blurPath, ct);

        return blurPath;
    }

    private void ScheduleTimer()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;

            var seconds = (int)_settings.RotationInterval;
            if (seconds <= 0) return;

            var period = TimeSpan.FromSeconds(seconds);
            _timer = new Timer(static state =>
            {
                var self = (WallpaperRotator)state!;
                _ = self.NextWallpaperAsync();
            }, this, period, period);
        }
    }

    public void Dispose() => Stop();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
