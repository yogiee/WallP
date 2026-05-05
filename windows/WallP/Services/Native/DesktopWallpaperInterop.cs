using System.Runtime.InteropServices;

namespace WallP.Services.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

internal enum DesktopWallpaperPosition
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

internal enum DesktopSlideshowOptions
{
    ShuffleImages = 0x01,
}

internal enum DesktopSlideshowState
{
    Enabled = 0x01,
    Slideshow = 0x02,
    DisabledByRemoteSession = 0x04,
}

internal enum DesktopSlideshowDirection
{
    Forward = 0,
    Backward = 1,
}

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
        [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);

    uint GetMonitorDevicePathCount();

    RECT GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

    void SetBackgroundColor(uint color);
    uint GetBackgroundColor();

    void SetPosition(DesktopWallpaperPosition position);
    DesktopWallpaperPosition GetPosition();

    void SetSlideshow(IntPtr items);
    IntPtr GetSlideshow();

    void SetSlideshowOptions(DesktopSlideshowOptions options, uint slideshowTick);

    [PreserveSig]
    int GetSlideshowOptions(out DesktopSlideshowOptions options, out uint slideshowTick);

    void AdvanceSlideshow(
        [MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
        DesktopSlideshowDirection direction);

    DesktopSlideshowState GetStatus();

    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

internal static class DesktopWallpaperFactory
{
    private static readonly Guid ClsidDesktopWallpaper = new("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");

    public static IDesktopWallpaper Create()
    {
        var type = Type.GetTypeFromCLSID(ClsidDesktopWallpaper)
            ?? throw new InvalidOperationException("Could not resolve DesktopWallpaper CLSID.");
        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Could not instantiate DesktopWallpaper COM object.");
        return (IDesktopWallpaper)instance;
    }
}
