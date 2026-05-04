using System.ComponentModel;
using System.Runtime.CompilerServices;
using WallP.Models;

namespace WallP.Services;

public sealed class WallpaperRotator : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly ImageCache _cache;
    private readonly DesktopWallpaperService _wallpaper;

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

    public void Start() => throw new NotImplementedException();
    public void Stop() => throw new NotImplementedException();
    public void Restart() => throw new NotImplementedException();

    public Task NextWallpaperAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task PreviousWallpaperAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task ShuffleAsync(CancellationToken ct = default) => throw new NotImplementedException();

    public void SwitchToCollection(Guid collectionId) => throw new NotImplementedException();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
