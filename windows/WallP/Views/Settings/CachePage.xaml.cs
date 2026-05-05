using System.Windows;
using System.Windows.Controls;
using WallP.Helpers;
using WallP.Models;
using WallP.Services;

namespace WallP.Views.Settings;

public partial class CachePage : Page
{
    private readonly AppSettings _settings;
    private readonly ImageCache _cache;
    private readonly WallpaperRotator _rotator;
    private bool _initializing = true;

    public CachePage(AppSettings settings, ImageCache cache, WallpaperRotator rotator)
    {
        _settings = settings;
        _cache = cache;
        _rotator = rotator;

        InitializeComponent();
        DataContext = settings;

        FormatPicker.ItemsSource = BuildFormatItems();
        // Older settings may have Heic stored — auto-migrate to JPEG since HEIC isn't
        // wired up yet and isn't in the picker.
        if (_settings.CacheImageFormat == ImageFormat.Heic)
        {
            _settings.CacheImageFormat = ImageFormat.Jpeg;
        }
        FormatPicker.SelectedValue = _settings.CacheImageFormat;
        UpdateFormatHint();

        CacheLimitPicker.ItemsSource = EnumPickerHelper.ItemsFor<CacheLimit>();
        CacheLimitPicker.SelectedValue = _settings.CacheLimit;

        BlurSlider.Value = _settings.BlurRadius;
        UpdateBlurLabel();

        RefreshStorage();

        _initializing = false;
    }

    private IList<EnumPickerItem<ImageFormat>> BuildFormatItems() => new List<EnumPickerItem<ImageFormat>>
    {
        new(ImageFormat.Jpeg, "JPEG (universal)"),
        new(ImageFormat.Webp, "WebP (~25% smaller, recommended)"),
    };

    private void UpdateFormatHint()
    {
        FormatHint.Text = "Used for newly-cached images. Existing files keep their format until re-synced.";
    }

    private void FormatPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (FormatPicker.SelectedValue is ImageFormat value)
        {
            _settings.CacheImageFormat = value;
        }
    }

    private void CacheLimitPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (CacheLimitPicker.SelectedValue is CacheLimit value)
        {
            _settings.CacheLimit = value;
        }
    }

    private CancellationTokenSource? _blurDebounceCts;

    private void BlurSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)Math.Round(e.NewValue);
        UpdateBlurLabel();
        if (_initializing) return;
        _settings.BlurRadius = value;

        // Debounce: every drag tick fires ValueChanged; we only want to re-apply once
        // the user settles on a value (~300ms idle).
        _blurDebounceCts?.Cancel();
        _blurDebounceCts = new CancellationTokenSource();
        var token = _blurDebounceCts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(300, token); }
            catch (TaskCanceledException) { return; }
            await Dispatcher.InvokeAsync(() => _rotator.ReapplyCurrentAsync());
        }, token);
    }

    private void UpdateBlurLabel()
    {
        var value = (int)Math.Round(BlurSlider.Value);
        BlurValueLabel.Text = value == 0 ? " — off" : $" — {value} px";
    }

    private async void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            "Delete all cached wallpapers?\n\nYou'll need to sync again before WallP can rotate.",
            "WallP",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        ClearAllButton.IsEnabled = false;
        try
        {
            await _cache.ClearAllAsync();
            _rotator.RefreshImageList();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Couldn't clear cache:\n\n{ex.Message}",
                "WallP",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            ClearAllButton.IsEnabled = true;
            RefreshStorage();
        }
    }

    private void RefreshStorage()
    {
        CacheSizeLabel.Text = _cache.FormattedSize();
        CachedCountLabel.Text = _settings.CachedImages.Count.ToString();
    }
}
