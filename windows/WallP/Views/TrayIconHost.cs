using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using WallP.Models;
using WallP.Services;

namespace WallP.Views;

public sealed class TrayIconHost : IDisposable
{
    private readonly AppSettings _settings;
    private readonly WallpaperRotator _rotator;
    private readonly SyncScheduler _sync;
    private TaskbarIcon? _icon;

    public TrayIconHost(
        AppSettings settings,
        WallpaperRotator rotator,
        SyncScheduler sync)
    {
        _settings = settings;
        _rotator = rotator;
        _sync = sync;
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

        _icon.LeftClickCommand = new RelayCommand(_ => OpenPopover());

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

    private static void OpenPopover()
    {
        // Dismiss any existing popover before showing a new one (rapid double-clicks).
        foreach (var existing in Application.Current.Windows.OfType<TrayPopover>().ToList())
        {
            existing.Close();
        }

        var popover = App.Services.GetRequiredService<TrayPopover>();
        popover.ShowNearTray();
    }
}

internal sealed class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action<object?> _execute;
    public RelayCommand(Action<object?> execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter);

    // ICommand requires this event; CanExecute is always true so we never raise it.
#pragma warning disable CS0067
    public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067
}
