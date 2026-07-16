using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Wpf.Ui.Controls;
using WallP.Models;
using WallP.Services;

namespace WallP.Views;

public partial class TrayPopover : FluentWindow
{
    private readonly AppSettings _settings;
    private readonly WallpaperRotator _rotator;
    private readonly SyncScheduler _sync;
    private readonly IServiceProvider _services;
    private bool _initializing = true;

    public TrayPopover(
        AppSettings settings,
        WallpaperRotator rotator,
        SyncScheduler sync,
        IServiceProvider services)
    {
        _settings = settings;
        _rotator = rotator;
        _sync = sync;
        _services = services;

        InitializeComponent();

        _rotator.PropertyChanged += OnRotatorChanged;
        _sync.PropertyChanged += OnSyncChanged;
        _settings.PropertyChanged += OnSettingsChanged;
        Closed += (_, _) =>
        {
            _rotator.PropertyChanged -= OnRotatorChanged;
            _sync.PropertyChanged -= OnSyncChanged;
            _settings.PropertyChanged -= OnSettingsChanged;
        };

        PopulateCollections();
        Refresh();

        _initializing = false;
    }

    /// <summary>
    /// Positions the popover at the bottom-right of the primary work area —
    /// roughly where the user's eye lands after clicking the tray icon.
    /// </summary>
    public void ShowNearTray()
    {
        const int Margin = 12;
        WindowStartupLocation = WindowStartupLocation.Manual;
        var work = SystemParameters.WorkArea;

        // We need actual height to position correctly. Show offscreen briefly so the
        // window measures itself, then snap into place.
        Left = -10000;
        Top = -10000;
        Show();
        Dispatcher.BeginInvoke(() =>
        {
            Left = work.Right - ActualWidth - Margin;
            Top = work.Bottom - ActualHeight - Margin;
            Activate();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private bool _closing;

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Guard against re-entry: Settings_Click already calls Close(), and the
        // resulting focus shift fires Deactivated which would call Close() again
        // mid-close — that's an InvalidOperationException.
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void OnRotatorChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.BeginInvoke(Refresh);

    private void OnSyncChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.BeginInvoke(Refresh);

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppSettings.IsPaused)
            or nameof(AppSettings.DefaultCollectionId)
            or nameof(AppSettings.DisplayOrder))
        {
            Dispatcher.BeginInvoke(Refresh);
        }
    }

    private void PopulateCollections()
    {
        CollectionPicker.ItemsSource = _settings.Collections.ToList();
        CollectionPicker.SelectedValue = _settings.DefaultCollectionId;
        CollectionPicker.Visibility = _settings.Collections.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Refresh()
    {
        var collection = _settings.ActiveCollection;
        if (collection is null)
        {
            CollectionLabel.Text = "No collection";
            CollectionMeta.Text = "Open Settings to add one";
        }
        else
        {
            CollectionLabel.Text = collection.Name;
            var count = _settings.ImagesForCollection(collection.Id).Count();
            CollectionMeta.Text = count == 1 ? "1 image" : $"{count} images";
        }

        // Status pill
        Brush pillBrush;
        string label;
        if (_sync.IsSyncing)
        {
            label = "Syncing";
            pillBrush = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // blue
        }
        else if (_settings.IsPaused)
        {
            label = "Paused";
            pillBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)); // gray
        }
        else if (_rotator.IsRunning)
        {
            label = "Running";
            pillBrush = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)); // green
        }
        else
        {
            label = "Idle";
            pillBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
        }
        StatusText.Text = label;
        StatusPill.Background = pillBrush;
        StatusText.Foreground = Brushes.White;

        // In random order the button shuffles; in a fixed order it advances to the
        // next wallpaper in sequence, so the icon/label reflect that.
        if (_settings.DisplayOrder == DisplayOrder.Random)
        {
            ShuffleIcon.Symbol = SymbolRegular.ArrowShuffle24;
            ShuffleLabel.Text = "Shuffle";
            ShuffleButton.ToolTip = "Shuffle wallpaper";
        }
        else
        {
            ShuffleIcon.Symbol = SymbolRegular.Next24;
            ShuffleLabel.Text = "Next";
            ShuffleButton.ToolTip = "Next wallpaper";
        }

        // Pause/Resume button
        if (_settings.IsPaused)
        {
            PlayPauseLabel.Text = "Resume";
            PlayPauseIcon.Symbol = SymbolRegular.Play24;
        }
        else
        {
            PlayPauseLabel.Text = "Pause";
            PlayPauseIcon.Symbol = SymbolRegular.Pause24;
        }

        // Setup-needed prompt or sync status
        var setupNeeded = string.IsNullOrWhiteSpace(_settings.ApiKey)
                          || _settings.Collections.Count == 0;
        if (setupNeeded)
        {
            StatusMessage.Text = "Set up your Wallhaven API key and add a collection in Settings.";
            StatusMessage.Visibility = Visibility.Visible;
            ShuffleButton.IsEnabled = false;
            SyncButton.IsEnabled = false;
        }
        else if (!string.IsNullOrEmpty(_sync.SyncProgress) && _sync.IsSyncing)
        {
            StatusMessage.Text = _sync.SyncProgress;
            StatusMessage.Visibility = Visibility.Visible;
            ShuffleButton.IsEnabled = !_settings.IsPaused;
            SyncButton.IsEnabled = !_sync.IsSyncing;
        }
        else if (!string.IsNullOrEmpty(_sync.LastSyncError))
        {
            StatusMessage.Text = _sync.LastSyncError;
            StatusMessage.Visibility = Visibility.Visible;
            ShuffleButton.IsEnabled = !_settings.IsPaused;
            SyncButton.IsEnabled = !_sync.IsSyncing;
        }
        else
        {
            StatusMessage.Visibility = Visibility.Collapsed;
            ShuffleButton.IsEnabled = !_settings.IsPaused;
            SyncButton.IsEnabled = !_sync.IsSyncing;
        }
    }

    private async void Shuffle_Click(object sender, RoutedEventArgs e)
    {
        try { await _rotator.ShuffleAsync(); } catch { /* swallow — UI only */ }
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_settings.IsPaused) _rotator.Resume(); else _rotator.Pause();
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        try { await _sync.SyncNowAsync(); } catch { /* swallow */ }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var existing = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
        }
        else
        {
            var window = _services.GetRequiredService<SettingsWindow>();
            window.Show();
            window.Activate();
        }
        // Don't Close() explicitly — the focus shift to the Settings window will
        // fire our Deactivated handler, which closes us once. Calling Close() here
        // races with that handler and is the same root cause as the prior NRE.
    }

    private void Quit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void CollectionPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (CollectionPicker.SelectedValue is Guid id && id != _settings.DefaultCollectionId)
        {
            _rotator.SwitchToCollection(id);
        }
    }
}
