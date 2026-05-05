using System.ComponentModel;

namespace WallP.Models;

public enum SyncInterval
{
    [Description("Every 1 hour")] OneHour = 3600,
    [Description("Every 2 hours")] TwoHours = 7200,
    [Description("Every 4 hours")] FourHours = 14400,
    [Description("Every 8 hours")] EightHours = 28800,
    [Description("Manual only")] Manual = 0,
}

public enum RotationInterval
{
    [Description("Every 5 minutes")] FiveMinutes = 300,
    [Description("Every 15 minutes")] FifteenMinutes = 900,
    [Description("Every 30 minutes")] ThirtyMinutes = 1800,
    [Description("Every 1 hour")] OneHour = 3600,
    [Description("Every 2 hours")] TwoHours = 7200,
    [Description("Every 4 hours")] FourHours = 14400,
}

public enum DisplayOrder
{
    [Description("Random / Shuffle")] Random,
    [Description("By Name")] Name,
    [Description("By Date Created")] DateCreated,
}

public enum CacheLimit
{
    [Description("50 images")] Fifty = 50,
    [Description("100 images")] Hundred = 100,
    [Description("200 images")] TwoHundred = 200,
    [Description("500 images")] FiveHundred = 500,
}

public enum ImageFormat
{
    [Description("JPEG (universal)")] Jpeg,
    [Description("WebP (~25% smaller than JPEG, recommended)")] Webp,
    // Heic is retained for backward-compat with older settings.json files; the
    // optimizer silently falls back to JPEG when this is selected, since HEIC
    // encoding requires the Microsoft HEIF Image Extension and isn't yet wired up.
    [Description("HEIC (not yet supported)")] Heic,
}

public enum UpdateMode
{
    [Description("Auto-update (recommended)")] Auto,
    [Description("Download updates, ask before installing")] Ask,
    [Description("Disabled")] Disabled,
}
