namespace WallP.Services;

public sealed class DesktopWallpaperService
{
    public Task SetWallpaperAsync(string monitorId, string imagePath, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task SetWallpaperAllMonitorsAsync(string imagePath, CancellationToken ct = default)
        => throw new NotImplementedException();

    public IReadOnlyList<MonitorInfo> GetMonitors()
        => throw new NotImplementedException();
}

public sealed record MonitorInfo(string Id, int Width, int Height, bool IsPrimary);
