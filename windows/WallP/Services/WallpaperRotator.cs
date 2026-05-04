using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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
        lock (_lock)
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

            _orderedImages = images;
            ReshuffleIndices();
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

        List<(MonitorInfo Monitor, CachedImage Image)> assignments;
        lock (_lock)
        {
            if (_orderedImages.Count == 0) return;

            if (monitors.Count > 1)
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
                // Single monitor: respect display order.
                if (_settings.DisplayOrder == DisplayOrder.Random)
                {
                    if (_shuffledIndices.Count == 0) ReshuffleIndices();
                    _currentIndex = _shuffledIndices[0];
                    _shuffledIndices.RemoveAt(0);
                }
                else
                {
                    _currentIndex = _orderedImages.Count == 0 ? 0 : (_currentIndex + 1) % _orderedImages.Count;
                }
                assignments = [(monitors[0], _orderedImages[_currentIndex])];
            }

            CurrentImageId = _orderedImages[_currentIndex].Id;
        }

        foreach (var (monitor, image) in assignments)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var path = _cache.PathFor(image);
                if (!File.Exists(path))
                {
                    Debug.WriteLine($"[WallP][Rotator] Missing on disk, skipping: {path}");
                    continue;
                }
                await _wallpaper.SetWallpaperAsync(monitor.Id, path, ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WallP][Rotator] SetWallpaper error on {monitor.Id}: {ex.Message}");
            }
        }
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

        var path = _cache.PathFor(image);
        if (!File.Exists(path))
        {
            Debug.WriteLine($"[WallP][Rotator] Previous: missing on disk: {path}");
            return;
        }

        try { await _wallpaper.SetWallpaperAllMonitorsAsync(path, ct); }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WallP][Rotator] Previous error: {ex.Message}");
        }
    }

    public Task ShuffleAsync(CancellationToken ct = default) => NextWallpaperAsync(ct);

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
