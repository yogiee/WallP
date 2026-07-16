using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WallP.Models;

public sealed class AppSettings : INotifyPropertyChanged
{
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WallP");

    public static string SettingsFile =>
        Path.Combine(SettingsDirectory, "settings.json");

    private string _apiKey = "";
    public string ApiKey
    {
        get => _apiKey;
        set => Set(ref _apiKey, value);
    }

    private string _wallhavenUsername = "";
    public string WallhavenUsername
    {
        get => _wallhavenUsername;
        set => Set(ref _wallhavenUsername, value);
    }

    private SyncInterval _syncInterval = SyncInterval.FourHours;
    public SyncInterval SyncInterval
    {
        get => _syncInterval;
        set => Set(ref _syncInterval, value);
    }

    private RotationInterval _rotationInterval = RotationInterval.ThirtyMinutes;
    public RotationInterval RotationInterval
    {
        get => _rotationInterval;
        set => Set(ref _rotationInterval, value);
    }

    private DisplayOrder _displayOrder = DisplayOrder.Random;
    public DisplayOrder DisplayOrder
    {
        get => _displayOrder;
        set => Set(ref _displayOrder, value);
    }

    private MultiMonitorMode _multiMonitorMode = MultiMonitorMode.DifferentPerMonitor;
    public MultiMonitorMode MultiMonitorMode
    {
        get => _multiMonitorMode;
        set => Set(ref _multiMonitorMode, value);
    }

    private CacheLimit _cacheLimit = CacheLimit.Hundred;
    public CacheLimit CacheLimit
    {
        get => _cacheLimit;
        set => Set(ref _cacheLimit, value);
    }

    private ImageFormat _cacheImageFormat = ImageFormat.Jpeg;
    public ImageFormat CacheImageFormat
    {
        get => _cacheImageFormat;
        set => Set(ref _cacheImageFormat, value);
    }

    private Guid? _defaultCollectionId;
    public Guid? DefaultCollectionId
    {
        get => _defaultCollectionId;
        set => Set(ref _defaultCollectionId, value);
    }

    private bool _optimizeImages = true;
    public bool OptimizeImages
    {
        get => _optimizeImages;
        set => Set(ref _optimizeImages, value);
    }

    private int _blurRadius;
    public int BlurRadius
    {
        get => _blurRadius;
        set => Set(ref _blurRadius, value);
    }

    private bool _pauseOnFullscreen = true;
    public bool PauseOnFullscreen
    {
        get => _pauseOnFullscreen;
        set => Set(ref _pauseOnFullscreen, value);
    }

    private bool _pauseOnBattery;
    public bool PauseOnBattery
    {
        get => _pauseOnBattery;
        set => Set(ref _pauseOnBattery, value);
    }

    private bool _respectMeteredNetwork = true;
    public bool RespectMeteredNetwork
    {
        get => _respectMeteredNetwork;
        set => Set(ref _respectMeteredNetwork, value);
    }

    private bool _showSyncCompleteToast = true;
    public bool ShowSyncCompleteToast
    {
        get => _showSyncCompleteToast;
        set => Set(ref _showSyncCompleteToast, value);
    }

    private bool _launchAtLogin;
    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set => Set(ref _launchAtLogin, value);
    }

    private UpdateMode _updateMode = UpdateMode.Auto;
    public UpdateMode UpdateMode
    {
        get => _updateMode;
        set => Set(ref _updateMode, value);
    }

    public List<WallPCollection> Collections { get; set; } = [];
    public List<CachedImage> CachedImages { get; set; } = [];

    [JsonIgnore] public bool IsPaused { get; set; }

    [JsonIgnore]
    public WallPCollection? ActiveCollection =>
        DefaultCollectionId is { } id
            ? Collections.FirstOrDefault(c => c.Id == id)
            : Collections.FirstOrDefault();

    public IEnumerable<CachedImage> ImagesForCollection(Guid collectionId) =>
        CachedImages.Where(i => i.CollectionId == collectionId);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null) return loaded;
            }
        }
        catch
        {
            // Corrupted settings file — fall through to defaults so the app still launches.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        Save();
    }
}
