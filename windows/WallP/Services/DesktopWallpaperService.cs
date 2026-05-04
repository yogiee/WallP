using System.IO;
using System.Runtime.InteropServices;
using WallP.Services.Native;

namespace WallP.Services;

public sealed record MonitorInfo(string Id, int Width, int Height, bool IsPrimary);

public sealed class DesktopWallpaperService
{
    private readonly Lock _lock = new();
    private IDesktopWallpaper? _com;

    private IDesktopWallpaper Com
    {
        get
        {
            lock (_lock)
            {
                _com ??= DesktopWallpaperFactory.Create();
                return _com;
            }
        }
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var result = new List<MonitorInfo>();
        var count = Com.GetMonitorDevicePathCount();
        var primaryFound = false;

        for (uint i = 0; i < count; i++)
        {
            var id = Com.GetMonitorDevicePathAt(i);
            if (string.IsNullOrEmpty(id)) continue;

            // GetMonitorRECT throws S_FALSE-ish for monitors that are present in the registry
            // but not currently attached. Skip those.
            try
            {
                var rect = Com.GetMonitorRECT(id);
                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                var isPrimary = !primaryFound && rect.Left == 0 && rect.Top == 0;
                if (isPrimary) primaryFound = true;
                result.Add(new MonitorInfo(id, width, height, isPrimary));
            }
            catch (COMException)
            {
                // Detached monitor — ignore.
            }
        }

        return result;
    }

    public Task SetWallpaperAsync(string monitorId, string imagePath, CancellationToken ct = default)
    {
        ValidateImagePath(imagePath);
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var com = Com;
            com.SetPosition(DesktopWallpaperPosition.Fill);
            com.SetWallpaper(monitorId, imagePath);
        }, ct);
    }

    public Task SetWallpaperAllMonitorsAsync(string imagePath, CancellationToken ct = default)
    {
        ValidateImagePath(imagePath);
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var com = Com;
            com.SetPosition(DesktopWallpaperPosition.Fill);
            // Passing null applies to every monitor in one call.
            com.SetWallpaper(null, imagePath);
        }, ct);
    }

    private static void ValidateImagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Image path cannot be empty.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Wallpaper image not found.", path);
    }
}
