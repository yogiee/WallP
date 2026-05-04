using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using WallP.Models;
using WallP.Services;

namespace WallP.Views;

public sealed class TrayIconHost : IDisposable
{
    private readonly AppSettings _settings;
    private readonly WallpaperRotator _rotator;
    private readonly SyncScheduler _sync;
    private readonly DesktopWallpaperService _wallpaper;
    private TaskbarIcon? _icon;

    public TrayIconHost(
        AppSettings settings,
        WallpaperRotator rotator,
        SyncScheduler sync,
        DesktopWallpaperService wallpaper)
    {
        _settings = settings;
        _rotator = rotator;
        _sync = sync;
        _wallpaper = wallpaper;
    }

    public void Show()
    {
        BitmapImage iconImage;
        try
        {
            iconImage = new BitmapImage(new Uri("pack://application:,,,/Assets/WallP.ico", UriKind.Absolute));
            iconImage.Freeze();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load tray icon resource:\n\n{ex.Message}",
                "WallP — tray icon",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            iconImage = null!;
        }

        _icon = new TaskbarIcon
        {
            ToolTipText = "WallP",
            IconSource = iconImage,
            Visibility = Visibility.Visible,
            ContextMenu = BuildMenu(),
        };

        _icon.LeftClickCommand = new RelayCommand(_ => OpenSettings());

        // H.NotifyIcon 2.x lazily registers with the shell. Force the registration so
        // the icon shows up immediately rather than (sometimes) waiting for a redraw.
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public void Dispose()
    {
        _icon?.Dispose();
        _icon = null;
    }

    private MenuItem? _pauseItem;

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        _pauseItem = MenuItem("Pause", _ => TogglePause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(MenuItem("Shuffle", async _ => await SafeRun(() => _rotator.ShuffleAsync())));
        menu.Items.Add(MenuItem("Sync now", async _ => await SafeRun(() => _sync.SyncNowAsync())));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Apply image…  (debug)", async _ => await ApplyImageFromPickerAsync()));
        menu.Items.Add(MenuItem("Add Wallhaven collection…  (debug)", _ => ShowAddCollectionDialog()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Settings…", _ => OpenSettings()));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItem("Quit WallP", _ => Application.Current.Shutdown()));

        // Refresh the Pause/Resume label every time the menu opens so it reflects
        // current state even if pause was toggled from elsewhere.
        menu.Opened += (_, _) =>
        {
            if (_pauseItem is not null)
                _pauseItem.Header = _settings.IsPaused ? "Resume" : "Pause";
        };

        return menu;
    }

    private void TogglePause()
    {
        if (_settings.IsPaused) _rotator.Resume(); else _rotator.Pause();
    }

    private void ShowAddCollectionDialog()
    {
        var dialog = new AddCollectionDialog(_settings)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsVisible),
        };

        if (dialog.ShowDialog() != true) return;

        var collection = new WallPCollection
        {
            Name = dialog.DisplayName,
            WallhavenCollectionId = dialog.CollectionId,
            WallhavenUsername = dialog.Username,
        };
        _settings.Collections.Add(collection);
        _settings.DefaultCollectionId ??= collection.Id;
        _settings.Save();

        // Kick off a sync of the new collection in the background.
        _ = Task.Run(async () =>
        {
            try { await _sync.SyncCollectionAsync(collection.Id); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[WallP] Initial sync error: {ex}"); }
        });
    }

    private async Task ApplyImageFromPickerAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Pick an image to set as wallpaper",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.heic;*.webp|All files|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var monitors = _wallpaper.GetMonitors();
            await _wallpaper.SetWallpaperAllMonitorsAsync(dialog.FileName);
            MessageBox.Show(
                $"Wallpaper applied to {monitors.Count} monitor(s).",
                "WallP",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to set wallpaper:\n\n{ex.Message}",
                "WallP",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static async Task SafeRun(Func<Task> action)
    {
        try { await action(); }
        catch (NotImplementedException)
        {
            MessageBox.Show(
                "Not yet implemented in this build.",
                "WallP",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "WallP", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static MenuItem MenuItem(string header, Action<object?> action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action(null);
        return item;
    }

    private static void OpenSettings()
    {
        var existing = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        var window = App.Services.GetRequiredService<SettingsWindow>();
        window.Show();
        window.Activate();
    }
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
}
