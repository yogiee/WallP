using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using WallP.Models;

namespace WallP.Services;

public sealed class ImageOptimizer
{
    private readonly AppSettings _settings;
    private readonly DesktopWallpaperService _wallpaper;

    public ImageOptimizer(AppSettings settings, DesktopWallpaperService wallpaper)
    {
        _settings = settings;
        _wallpaper = wallpaper;
    }

    /// <summary>
    /// Optimizes a source image and writes the result into <paramref name="destinationDirectory"/>
    /// using <paramref name="baseFileName"/> + the format-appropriate extension.
    /// Returns the absolute path of the written file.
    /// </summary>
    public async Task<string> OptimizeAsync(
        string sourceFilePath,
        string destinationDirectory,
        string baseFileName,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(destinationDirectory);

        // HEIC encoding requires WIC + the Microsoft HEIF Image Extension. Not wired up
        // yet — silently fall back to JPEG when HEIC is selected (kept for back-compat
        // with older settings.json that may still hold an Heic value).
        var format = _settings.CacheImageFormat;
        if (format == ImageFormat.Heic) format = ImageFormat.Jpeg;

        var extension = format switch
        {
            ImageFormat.Webp => ".webp",
            _ => ".jpg",
        };
        var destinationPath = Path.Combine(destinationDirectory, baseFileName + extension);

        // Resolve target dimensions from the actual attached monitors. Falls back to a
        // sane 4K-ish ceiling if the COM enumeration is empty (rare, e.g., Remote Desktop
        // sessions before a monitor has been claimed).
        var monitors = _wallpaper.GetMonitors();
        int targetMaxDim;
        double screenAspect;
        if (monitors.Count > 0)
        {
            targetMaxDim = monitors.Max(m => Math.Max(m.Width, m.Height));
            screenAspect = monitors.Max(m => (double)m.Width / Math.Max(1, m.Height));
        }
        else
        {
            targetMaxDim = 3840;
            screenAspect = 16.0 / 9.0;
        }

        using var image = await Image.LoadAsync(sourceFilePath, ct);

        // Downscale via Lanczos when meaningfully larger than the screen — the 1.05x slack
        // matches the Mac optimizer and avoids re-encoding images that are barely over.
        var maxDim = Math.Max(image.Width, image.Height);
        if (maxDim > targetMaxDim * 1.05)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(targetMaxDim, targetMaxDim),
                Sampler = KnownResamplers.Lanczos3,
            }));
        }

        // Center-crop portrait/narrow images to the screen aspect ratio. Windows fills
        // the screen height and clips horizontal overflow when the image is narrower than
        // the screen — without this crop, narrow images get pillar-box bars.
        var imgAspect = (double)image.Width / image.Height;
        if (imgAspect < screenAspect - 0.05)
        {
            var cropHeight = (int)Math.Round(image.Width / screenAspect);
            if (cropHeight > 0 && cropHeight < image.Height)
            {
                var yOffset = (image.Height - cropHeight) / 2;
                image.Mutate(x => x.Crop(new Rectangle(0, yOffset, image.Width, cropHeight)));
            }
        }

        if (_settings.BlurRadius > 0)
        {
            image.Mutate(x => x.GaussianBlur(_settings.BlurRadius));
        }

        // Encoder selection: WebP at q=85 lands very close to HEIC's compression ratio
        // while remaining royalty-free; JPEG at q=90 is the universal fallback.
        if (format == ImageFormat.Webp)
        {
            var webpEncoder = new WebpEncoder { Quality = 85, FileFormat = WebpFileFormatType.Lossy };
            await image.SaveAsync(destinationPath, webpEncoder, ct);
        }
        else
        {
            var jpegEncoder = new JpegEncoder { Quality = 90 };
            await image.SaveAsync(destinationPath, jpegEncoder, ct);
        }

        return destinationPath;
    }

    /// <summary>
    /// Whether HEIC encoding is available on this machine. Currently always false —
    /// will probe the WIC HEIF encoder once HEIC support is wired up.
    /// </summary>
    public static bool IsHeicAvailable() => false;
}
